using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Domain.Entities.Reservas;

namespace GIRA.Application.Services.Reservas;

/// <summary>
/// Implementación de <see cref="IDisponibilidadService"/>. Para el cliente devuelve TODAS las franjas activas del
/// día, cada una con su disponibilidad y, si no se puede elegir, el motivo (GI-226); el frontend decide cómo
/// presentarlas. Para el personal conserva su regla: solo las franjas que todavía se pueden reservar, sin la
/// antelación mínima.
/// </summary>
public class DisponibilidadService : IDisponibilidadService
{
    private readonly IMesaRepository _mesaRepository;
    private readonly IFranjaHorariaRepository _franjaHorariaRepository;
    private readonly IReservaRepository _reservaRepository;
    private readonly IParametrosReservaService _parametros;
    private readonly IValidator<ConsultarDisponibilidadRequest> _validator;
    private readonly TimeProvider _timeProvider;

    /// <summary>Crea el servicio con sus repositorios, los parámetros del restaurante y el reloj.</summary>
    public DisponibilidadService(
        IMesaRepository mesaRepository,
        IFranjaHorariaRepository franjaHorariaRepository,
        IReservaRepository reservaRepository,
        IParametrosReservaService parametros,
        IValidator<ConsultarDisponibilidadRequest> validator,
        TimeProvider timeProvider)
    {
        _mesaRepository = mesaRepository;
        _franjaHorariaRepository = franjaHorariaRepository;
        _reservaRepository = reservaRepository;
        _parametros = parametros;
        _validator = validator;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async Task<OperationResult<IReadOnlyList<FranjaDisponibleDto>>> ConsultarAsync(
        ConsultarDisponibilidadRequest request, CancellationToken cancellationToken,
        AudienciaReserva audiencia = AudienciaReserva.Cliente)
    {
        // Las reglas de fecha (pasada, fuera de la ventana) viven en el RuleSet "Fecha": el personal las conserva
        // como error 400; para el cliente son franjas deshabilitadas con su motivo, no un error.
        var validacion = audiencia == AudienciaReserva.Personal
            ? await _validator.ValidateAsync(request, opciones => opciones.IncludeAllRuleSets(), cancellationToken)
            : await _validator.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<IReadOnlyList<FranjaDisponibleDto>>(
                ErrorType.Validation, validacion.ToString());
        }

        var parametros = await _parametros.ObtenerAsync(cancellationToken);
        var mesas = await _mesaRepository.ListarActivasAsync(cancellationToken);

        // Tamaño efectivo del grupo: el configurado, sin pasar de la mesa activa más grande.
        if (mesas.Count > 0)
        {
            var tamanoMaximo = Math.Min(parametros.MaxPersonasPorReserva, mesas.Max(m => m.Capacidad));
            if (request.Personas > tamanoMaximo)
            {
                return OperationResult.Failure<IReadOnlyList<FranjaDisponibleDto>>(
                    ErrorType.Validation,
                    MensajesReservas.GrupoGrande(audiencia, tamanoMaximo));
            }
        }

        var franjas = (await _franjaHorariaRepository.ListarActivasAsync(cancellationToken))
            .OrderBy(f => f.HoraInicio)
            .ToList();

        return audiencia == AudienciaReserva.Personal
            ? await ConsultarPersonalAsync(request, parametros, mesas, franjas, cancellationToken)
            : await ConsultarClienteAsync(request, parametros, mesas, franjas, cancellationToken);
    }

    // Cliente: todas las franjas activas; las que no se pueden elegir salen con su motivo.
    private async Task<OperationResult<IReadOnlyList<FranjaDisponibleDto>>> ConsultarClienteAsync(
        ConsultarDisponibilidadRequest request, ParametrosReserva parametros, IReadOnlyList<Mesa> mesas,
        IReadOnlyList<FranjaHoraria> franjas, CancellationToken cancellationToken)
    {
        var ocupadas = await _reservaRepository.ListarVigentesPorFechaAsync(request.Fecha, cancellationToken);
        var mesasConCapacidad = mesas.Where(m => m.Capacidad >= request.Personas).ToList();
        var ahoraLocal = _timeProvider.AhoraLocal();
        var hoy = DateOnly.FromDateTime(ahoraLocal);

        var resultado = new List<FranjaDisponibleDto>();
        foreach (var franja in franjas)
        {
            var motivo = MotivoNoDisponibleCliente(
                request, parametros, franja, ahoraLocal, hoy, mesasConCapacidad, ocupadas);
            resultado.Add(new FranjaDisponibleDto(
                franja.Id, franja.Nombre, franja.HoraInicio, franja.HoraFin, motivo is null, motivo));
        }

        return OperationResult.Success<IReadOnlyList<FranjaDisponibleDto>>(resultado);
    }

    // Orden de los motivos: restaurante cerrado, fuera de la ventana de reserva, franja que ya pasó, antelación
    // mínima y, al final, sin mesas. Devuelve null si la franja se puede elegir.
    private static string? MotivoNoDisponibleCliente(
        ConsultarDisponibilidadRequest request, ParametrosReserva parametros, FranjaHoraria franja,
        DateTime ahoraLocal, DateOnly hoy, IReadOnlyList<Mesa> mesasConCapacidad, IReadOnlyList<Reserva> ocupadas)
    {
        if (!parametros.EstaAbierto(request.Fecha))
        {
            return MensajesReservas.RestauranteCerrado;
        }

        if (request.Fecha > hoy.AddDays(parametros.AntelacionMaxima.Days))
        {
            return MensajesReservas.FueraDelRango(parametros.AntelacionMaxima.Days);
        }

        // Fecha pasada, o franja de hoy que ya terminó (el fin exacto cuenta como terminada).
        if (request.Fecha < hoy || ahoraLocal >= request.Fecha.ToDateTime(franja.HoraFin))
        {
            return MensajesReservas.FranjaYaPaso;
        }

        // Antelación mínima desde el inicio; también cubre la franja de hoy que está en curso.
        if (request.Fecha.ToDateTime(franja.HoraInicio) < ahoraLocal + parametros.AntelacionMinima)
        {
            return MensajesReservas.RequiereAntelacion(parametros.AntelacionMinima);
        }

        var hayMesaLibre = mesasConCapacidad.Any(mesa =>
            !ocupadas.Any(reserva => reserva.MesaId == mesa.Id && reserva.FranjaHorariaId == franja.Id));

        return hayMesaLibre ? null : MensajesReservas.SinMesas(request.Personas);
    }

    // Personal (GI-403): sin la antelación mínima; solo se excluyen las franjas terminadas y, como antes, un día
    // cerrado o la falta de mesas dan una lista vacía.
    private async Task<OperationResult<IReadOnlyList<FranjaDisponibleDto>>> ConsultarPersonalAsync(
        ConsultarDisponibilidadRequest request, ParametrosReserva parametros, IReadOnlyList<Mesa> mesas,
        IReadOnlyList<FranjaHoraria> franjas, CancellationToken cancellationToken)
    {
        if (mesas.Count == 0 || !parametros.EstaAbierto(request.Fecha))
        {
            return OperationResult.Success<IReadOnlyList<FranjaDisponibleDto>>(Array.Empty<FranjaDisponibleDto>());
        }

        var mesasConCapacidad = mesas.Where(m => m.Capacidad >= request.Personas).ToList();
        if (mesasConCapacidad.Count == 0)
        {
            return OperationResult.Success<IReadOnlyList<FranjaDisponibleDto>>(Array.Empty<FranjaDisponibleDto>());
        }

        var ocupadas = await _reservaRepository.ListarVigentesPorFechaAsync(request.Fecha, cancellationToken);
        var ahoraLocal = _timeProvider.AhoraLocal();
        var disponibles = new List<FranjaDisponibleDto>();

        foreach (var franja in franjas)
        {
            // El fin exacto cuenta como terminada, igual que en el check-in.
            if (ahoraLocal >= request.Fecha.ToDateTime(franja.HoraFin))
            {
                continue;
            }

            var hayMesaLibre = mesasConCapacidad.Any(mesa =>
                !ocupadas.Any(reserva => reserva.MesaId == mesa.Id && reserva.FranjaHorariaId == franja.Id));

            if (hayMesaLibre)
            {
                disponibles.Add(new FranjaDisponibleDto(franja.Id, franja.Nombre, franja.HoraInicio, franja.HoraFin));
            }
        }

        return OperationResult.Success<IReadOnlyList<FranjaDisponibleDto>>(disponibles);
    }
}
