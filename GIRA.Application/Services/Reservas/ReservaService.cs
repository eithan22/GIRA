using System.Globalization;
using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Events.Reservas;
using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Application.Validations.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Domain.Exceptions.Reservas;
using MediatR;

namespace GIRA.Application.Services.Reservas;

/// <summary>Implementación de <see cref="IReservaService"/>: crea reservas como invitado.</summary>
public class ReservaService : IReservaService
{
    private readonly IMesaRepository _mesaRepository;
    private readonly IFranjaHorariaRepository _franjaHorariaRepository;
    private readonly IReservaRepository _reservaRepository;
    private readonly IClienteRepository _clienteRepository;
    private readonly IMesaGestionRepository _mesaGestionRepository;
    private readonly IParametrosReservaService _parametros;
    private readonly IValidator<CrearReservaRequest> _validator;
    private readonly IValidator<ListarReservasRequest> _validadorListado;
    private readonly IValidator<CheckInRequest> _validadorCheckIn;
    private readonly GeneradorCodigoReserva _generadorCodigo;
    private readonly IPublisher _publisher;
    private readonly TimeProvider _timeProvider;

    // Mismo mensaje para "código no existe" y "teléfono no coincide": nunca se revela cuál falló.
    private const string MensajeCodigoOTelefonoIncorrectos = "Código o teléfono incorrectos.";

    /// <summary>Crea el servicio con sus repositorios, dependencias y el reloj.</summary>
    public ReservaService(
        IMesaRepository mesaRepository,
        IFranjaHorariaRepository franjaHorariaRepository,
        IReservaRepository reservaRepository,
        IClienteRepository clienteRepository,
        IMesaGestionRepository mesaGestionRepository,
        IParametrosReservaService parametros,
        IValidator<CrearReservaRequest> validator,
        IValidator<ListarReservasRequest> validadorListado,
        IValidator<CheckInRequest> validadorCheckIn,
        GeneradorCodigoReserva generadorCodigo,
        IPublisher publisher,
        TimeProvider timeProvider)
    {
        _mesaRepository = mesaRepository;
        _franjaHorariaRepository = franjaHorariaRepository;
        _reservaRepository = reservaRepository;
        _clienteRepository = clienteRepository;
        _mesaGestionRepository = mesaGestionRepository;
        _parametros = parametros;
        _validator = validator;
        _validadorListado = validadorListado;
        _validadorCheckIn = validadorCheckIn;
        _generadorCodigo = generadorCodigo;
        _publisher = publisher;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public Task<OperationResult<ReservaCreadaDto>> CrearAsync(CrearReservaRequest request, CancellationToken cancellationToken) =>
        CrearInternoAsync(request, OrigenReserva.Web, cancellationToken);

    /// <inheritdoc />
    public Task<OperationResult<ReservaCreadaDto>> CrearComoPersonalAsync(
        CrearReservaRequest request, OrigenReserva origen, CancellationToken cancellationToken)
    {
        if (origen is not (OrigenReserva.Telefono or OrigenReserva.Presencial))
        {
            return Task.FromResult(OperationResult.Failure<ReservaCreadaDto>(
                ErrorType.Validation, "El origen debe ser Telefono o Presencial."));
        }

        return CrearInternoAsync(request, origen, cancellationToken);
    }

    // Lógica compartida por CrearAsync (invitado, Origen = Web) y CrearComoPersonalAsync
    // (personal, Origen = Telefono/Presencial): mismas validaciones y asignación de mesa
    // para ambos caminos; solo cambia qué Origen queda en la reserva.
    private async Task<OperationResult<ReservaCreadaDto>> CrearInternoAsync(
        CrearReservaRequest request, OrigenReserva origen, CancellationToken cancellationToken)
    {
        var validacion = await _validator.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<ReservaCreadaDto>(ErrorType.Validation, validacion.ToString());
        }

        var telefono = TelefonoRd.Normalizar(request.Telefono);

        // --- Todo lo que no necesita al cliente, primero: así no se crea un cliente por una
        // reserva que de todos modos no era posible. ---

        var mesas = await _mesaRepository.ListarActivasAsync(cancellationToken);
        var capacidadMaxima = mesas.Count == 0 ? 0 : mesas.Max(m => m.Capacidad);
        if (request.Personas > capacidadMaxima)
        {
            return OperationResult.Failure<ReservaCreadaDto>(
                ErrorType.Validation,
                $"Para grupos de más de {capacidadMaxima} personas, llama al restaurante.");
        }

        var franjas = await _franjaHorariaRepository.ListarActivasAsync(cancellationToken);
        var franja = franjas.FirstOrDefault(f => f.Id == request.FranjaHorariaId);
        if (franja is null)
        {
            return OperationResult.Failure<ReservaCreadaDto>(
                ErrorType.Validation, "La franja horaria no existe o no está activa.");
        }

        if (_parametros.DiasCerrados.Contains(request.Fecha.DayOfWeek))
        {
            return OperationResult.Failure<ReservaCreadaDto>(
                ErrorType.Validation, "El restaurante está cerrado ese día.");
        }

        var ahoraLocal = _timeProvider.AhoraLocal();
        var inicioFranjaLocal = request.Fecha.ToDateTime(franja.HoraInicio);
        if (inicioFranjaLocal < ahoraLocal + _parametros.AntelacionMinima)
        {
            return OperationResult.Failure<ReservaCreadaDto>(
                ErrorType.Validation, "Esa franja ya no tiene la antelación mínima requerida.");
        }

        var ocupadas = await _reservaRepository.ListarVigentesPorFechaAsync(request.Fecha, cancellationToken);
        var mesasOcupadasIds = ocupadas
            .Where(r => r.FranjaHorariaId == franja.Id)
            .Select(r => r.MesaId)
            .ToHashSet();

        var mesaElegida = mesas
            .Where(m => m.Capacidad >= request.Personas)
            .OrderBy(m => m.Capacidad)
            .FirstOrDefault(m => !mesasOcupadasIds.Contains(m.Id));

        if (mesaElegida is null)
        {
            return OperationResult.Failure<ReservaCreadaDto>(
                ErrorType.Conflict, "Esa mesa ya fue tomada, intenta otra franja.");
        }

        // Limitación conocida y aceptada: esta cuenta y el guardado final no están en la misma
        // transacción, así que dos solicitudes del mismo teléfono a la vez podrían dejar pasar
        // una tercera reserva vigente. No hay índice ni restricción de base que lo evite hoy.
        var vigentesDelTelefono = await _reservaRepository.ContarVigentesPorTelefonoAsync(telefono, cancellationToken);
        if (vigentesDelTelefono >= 2)
        {
            return OperationResult.Failure<ReservaCreadaDto>(
                ErrorType.Conflict,
                "Ya tienes 2 reservas vigentes con este teléfono; cancela una para reservar otra.");
        }

        // --- Solo ahora, con la reserva confirmada como posible, se toca al cliente. Se guarda
        // en su propio SaveChanges (antes de construir la reserva) para poder recuperarse si
        // otra solicitud crea, al mismo tiempo, un cliente con el mismo teléfono. Si después la
        // mesa se pierde por otra carrera, puede quedar un cliente sin reserva: aceptado, sin
        // impacto funcional (el próximo intento con el mismo teléfono lo encuentra y lo usa). ---

        var cliente = await _clienteRepository.BuscarPorTelefonoAsync(telefono, cancellationToken);
        if (cliente is null)
        {
            cliente = new Cliente { Id = Guid.NewGuid(), Nombre = request.Nombre, Telefono = telefono };
            _clienteRepository.Agregar(cliente);
            try
            {
                await _clienteRepository.GuardarCambiosAsync(cancellationToken);
            }
            catch (ClienteDuplicadoException)
            {
                cliente = await _clienteRepository.BuscarPorTelefonoAsync(telefono, cancellationToken);
                if (cliente is null)
                {
                    // La base dice que hubo un choque de teléfono, pero la nueva búsqueda no
                    // encuentra a nadie (por ejemplo, un borrado lógico que el filtro global
                    // oculta pero el índice único sigue contando). No se reintenta en bucle.
                    return OperationResult.Failure<ReservaCreadaDto>(
                        ErrorType.Conflict,
                        "No se pudo registrar el cliente. Intenta de nuevo en unos minutos.");
                }
            }
        }
        else if (cliente.Nombre != request.Nombre)
        {
            // Decisión de negocio: el nombre del cliente se actualiza al más reciente.
            cliente.Nombre = request.Nombre;
        }

        string codigo;
        var intentos = 0;
        bool codigoLibre;
        do
        {
            intentos++;
            if (intentos > 5)
            {
                return OperationResult.Failure<ReservaCreadaDto>(
                    ErrorType.Conflict, "No se pudo generar un código único para la reserva. Intenta de nuevo.");
            }

            codigo = _generadorCodigo.Generar();
            codigoLibre = !await _reservaRepository.ExisteCodigoAsync(codigo, cancellationToken);
        }
        while (!codigoLibre);

        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Codigo = codigo,
            ClienteId = cliente.Id,
            MesaId = mesaElegida.Id,
            FranjaHorariaId = franja.Id,
            Fecha = request.Fecha,
            Personas = request.Personas,
            Origen = origen
        };

        _reservaRepository.Agregar(reserva);

        try
        {
            await _reservaRepository.GuardarCambiosAsync(cancellationToken);
        }
        catch (ReservaDuplicadaException ex)
        {
            return OperationResult.Failure<ReservaCreadaDto>(ErrorType.Conflict, ex.Message);
        }

        await _publisher.Publish(
            new ReservaConfirmadaEvent(
                reserva.Id, reserva.Codigo, cliente.Id, mesaElegida.Id, franja.Id,
                reserva.Fecha, reserva.Personas, reserva.Origen),
            cancellationToken);

        return OperationResult.Success(new ReservaCreadaDto(
            reserva.Codigo, cliente.Nombre, reserva.Fecha, franja.Nombre, franja.HoraInicio, franja.HoraFin, reserva.Personas));
    }

    /// <inheritdoc />
    public async Task<OperationResult<PagedResult<ReservaListadoDto>>> ListarAsync(
        ListarReservasRequest request, CancellationToken cancellationToken)
    {
        var validacion = await _validadorListado.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<PagedResult<ReservaListadoDto>>(ErrorType.Validation, validacion.ToString());
        }

        var telefono = string.IsNullOrWhiteSpace(request.Telefono) ? null : TelefonoRd.Normalizar(request.Telefono);

        // Sin fecha ni teléfono, la agenda es la de hoy: nunca se devuelve toda la historia por accidente.
        var fecha = request.Fecha ?? (telefono is null ? _timeProvider.HoyLocal() : null);

        EstadoReserva? estado = ListarReservasRequestValidator.TryParseEstado(request.Estado, out var parseado) ? parseado : null;

        var pagina = await _reservaRepository.ListarAsync(
            fecha, estado, request.FranjaId, telefono, request.Page, request.PageSize, cancellationToken);

        return OperationResult.Success(pagina);
    }

    /// <inheritdoc />
    public async Task<OperationResult<ReservaDetalleDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken)
    {
        var detalle = await _reservaRepository.ObtenerDetalleAsync(id, cancellationToken);

        return detalle is null
            ? OperationResult.Failure<ReservaDetalleDto>(ErrorType.NotFound, "No encontramos esa reserva.")
            : OperationResult.Success(detalle);
    }

    /// <inheritdoc />
    public async Task<OperationResult<ReservaDetalleDto>> CheckInAsync(
        Guid id, CheckInRequest request, CancellationToken cancellationToken)
    {
        var validacion = await _validadorCheckIn.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<ReservaDetalleDto>(ErrorType.Validation, validacion.ToString());
        }

        var reserva = await _reservaRepository.BuscarPorIdAsync(id, cancellationToken);
        if (reserva is null)
        {
            return OperationResult.Failure<ReservaDetalleDto>(ErrorType.NotFound, "No encontramos esa reserva.");
        }

        if (reserva.Estado != EstadoReserva.Confirmada)
        {
            return OperationResult.Failure<ReservaDetalleDto>(ErrorType.Conflict, MensajeCheckInNoPermitido(reserva.Estado));
        }

        // Si esto fuera null sería una violación de integridad (ver ConsultarPorCodigoAsync).
        var franja = await _franjaHorariaRepository.BuscarPorIdAsync(reserva.FranjaHorariaId, cancellationToken)
            ?? throw new InvalidOperationException($"La reserva {reserva.Codigo} referencia una franja que no existe.");

        var ahoraLocal = _timeProvider.AhoraLocal();
        var aperturaCheckIn = reserva.Fecha.ToDateTime(franja.HoraInicio) - _parametros.VentanaCheckIn;
        if (ahoraLocal < aperturaCheckIn)
        {
            var hora = aperturaCheckIn.ToString("HH:mm", CultureInfo.InvariantCulture);
            var mensaje = aperturaCheckIn.Date == ahoraLocal.Date
                ? $"El check-in se habilita a partir de las {hora}."
                : $"El check-in se habilita el {aperturaCheckIn.ToString("dd/MM", CultureInfo.InvariantCulture)} a partir de las {hora}.";
            return OperationResult.Failure<ReservaDetalleDto>(ErrorType.Conflict, mensaje);
        }

        // Límite superior: desde el fin de la franja ya no se puede (el cierre automático la completaría).
        if (ahoraLocal >= reserva.Fecha.ToDateTime(franja.HoraFin))
        {
            return OperationResult.Failure<ReservaDetalleDto>(
                ErrorType.Conflict, "La franja de esta reserva ya terminó; no se puede hacer check-in.");
        }

        // Si esto fuera null sería una violación de integridad: la reserva siempre tiene mesa
        // (la mesa borrada lógicamente tampoco aparece, y una reserva no puede quedar huérfana).
        var mesa = await _mesaGestionRepository.BuscarPorIdAsync(reserva.MesaId, cancellationToken)
            ?? throw new InvalidOperationException($"La reserva {reserva.Codigo} referencia una mesa que no existe.");

        if (request.PersonasReales > mesa.Capacidad)
        {
            return OperationResult.Failure<ReservaDetalleDto>(
                ErrorType.Validation, $"La mesa {mesa.Numero} admite como máximo {mesa.Capacidad} personas.");
        }

        reserva.HacerCheckIn(request.PersonasReales, _timeProvider.GetUtcNow().UtcDateTime);

        try
        {
            await _reservaRepository.GuardarCambiosAsync(cancellationToken);
        }
        catch (ReservaConflictoConcurrenciaException)
        {
            // Mensaje propio: el del repositorio habla de la cancelación y la pantalla pública lo
            // compara por subcadena, así que no se toca.
            return OperationResult.Failure<ReservaDetalleDto>(
                ErrorType.Conflict, "La reserva cambió mientras se procesaba el check-in; vuelve a consultarla.");
        }

        var detalle = await _reservaRepository.ObtenerDetalleAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"La reserva {reserva.Codigo} desapareció después del check-in.");

        return OperationResult.Success(detalle);
    }

    private static string MensajeCheckInNoPermitido(EstadoReserva estado) => estado switch
    {
        EstadoReserva.EnCurso => "Esta reserva ya tiene el check-in registrado.",
        EstadoReserva.Cancelada => "La reserva fue cancelada; no se puede hacer check-in.",
        EstadoReserva.NoAsistio => "La reserva fue marcada como no asistida. Si el cliente llegó, regístralo como visita sin reserva.",
        EstadoReserva.Completada => "La reserva ya fue completada.",
        _ => "Solo se puede hacer check-in a una reserva Confirmada."
    };

    /// <inheritdoc />
    public async Task<OperationResult<ReservaConsultadaDto>> ConsultarPorCodigoAsync(
        ConsultarReservaRequest request, CancellationToken cancellationToken)
    {
        // Request null, cuerpo vacío o con campos en blanco: mismo 404, sin tocar TelefonoRd ni la base.
        if (request is null || string.IsNullOrWhiteSpace(request.Codigo) || string.IsNullOrWhiteSpace(request.Telefono))
        {
            return OperationResult.Failure<ReservaConsultadaDto>(ErrorType.NotFound, MensajeCodigoOTelefonoIncorrectos);
        }

        var codigo = request.Codigo.Trim();
        var telefono = TelefonoRd.Normalizar(request.Telefono);

        var reserva = await _reservaRepository.BuscarPorCodigoYTelefonoAsync(codigo, telefono, cancellationToken);
        if (reserva is null)
        {
            return OperationResult.Failure<ReservaConsultadaDto>(ErrorType.NotFound, MensajeCodigoOTelefonoIncorrectos);
        }

        // Si esto fuera null sería una violación de integridad (una reserva siempre referencia
        // un cliente y una franja válidos); se deja fallar fuerte en vez de devolver datos vacíos.
        var cliente = await _clienteRepository.BuscarPorIdAsync(reserva.ClienteId, cancellationToken)
            ?? throw new InvalidOperationException($"La reserva {reserva.Codigo} referencia un cliente que no existe.");
        var franja = await _franjaHorariaRepository.BuscarPorIdAsync(reserva.FranjaHorariaId, cancellationToken)
            ?? throw new InvalidOperationException($"La reserva {reserva.Codigo} referencia una franja que no existe.");

        return OperationResult.Success(new ReservaConsultadaDto(
            reserva.Codigo, cliente.Nombre, reserva.Fecha, franja.Nombre, franja.HoraInicio, franja.HoraFin,
            reserva.Personas, reserva.Estado.ToString()));
    }

    /// <inheritdoc />
    public async Task<OperationResult> CancelarPorCodigoAsync(
        string codigo, CancelarReservaRequest request, CancellationToken cancellationToken)
    {
        // Mismo 404 neutro que ConsultarPorCodigoAsync: sin tocar TelefonoRd ni la base.
        if (request is null || string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(request.Telefono))
        {
            return OperationResult.Failure(ErrorType.NotFound, MensajeCodigoOTelefonoIncorrectos);
        }

        var codigoNormalizado = codigo.Trim();
        var telefono = TelefonoRd.Normalizar(request.Telefono);

        var reserva = await _reservaRepository.BuscarPorCodigoYTelefonoAsync(codigoNormalizado, telefono, cancellationToken);
        if (reserva is null)
        {
            return OperationResult.Failure(ErrorType.NotFound, MensajeCodigoOTelefonoIncorrectos);
        }

        if (reserva.Estado != EstadoReserva.Confirmada)
        {
            return OperationResult.Failure(ErrorType.Conflict, "Esta reserva ya no se puede cancelar.");
        }

        // Si esto fuera null sería una violación de integridad (ver ConsultarPorCodigoAsync).
        var franja = await _franjaHorariaRepository.BuscarPorIdAsync(reserva.FranjaHorariaId, cancellationToken)
            ?? throw new InvalidOperationException($"La reserva {reserva.Codigo} referencia una franja que no existe.");

        var inicioFranjaLocal = reserva.Fecha.ToDateTime(franja.HoraInicio);
        var ahoraLocal = _timeProvider.AhoraLocal();
        if (inicioFranjaLocal - ahoraLocal < _parametros.LimiteCancelacion)
        {
            return OperationResult.Failure(
                ErrorType.Conflict,
                $"Ya no se puede cancelar esta reserva; debe hacerse con al menos {FormatearDuracion(_parametros.LimiteCancelacion)} de anticipación.");
        }

        reserva.Cancelar(null);

        try
        {
            await _reservaRepository.GuardarCambiosAsync(cancellationToken);
        }
        catch (ReservaConflictoConcurrenciaException ex)
        {
            return OperationResult.Failure(ErrorType.Conflict, ex.Message);
        }

        await _publisher.Publish(
            new ReservaCanceladaEvent(reserva.Id, reserva.MotivoCancelacion), cancellationToken);

        return OperationResult.Success();
    }

    /// <summary>Formatea una duración como texto legible en español: "2 horas" o "90 minutos".</summary>
    private static string FormatearDuracion(TimeSpan duracion)
    {
        if (duracion.Days == 0 && duracion.Minutes == 0 && duracion.Hours > 0)
        {
            return duracion.Hours == 1 ? "1 hora" : $"{duracion.Hours} horas";
        }

        var minutosTotales = (int)duracion.TotalMinutes;
        return minutosTotales == 1 ? "1 minuto" : $"{minutosTotales} minutos";
    }
}
