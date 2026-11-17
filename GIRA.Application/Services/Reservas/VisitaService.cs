using System.Globalization;
using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Events.Reservas;
using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Domain.Entities.Reservas;

namespace GIRA.Application.Services.Reservas;

/// <summary>Implementación de <see cref="IVisitaService"/>.</summary>
public class VisitaService : IVisitaService
{
    /// <summary>Máximo de visitas que trae el listado del día (GI-402); los totales cuentan todas.</summary>
    public const int TopeListado = 200;

    private readonly IVisitaRepository _visitas;
    private readonly IFranjaGestionRepository _franjas;
    private readonly IMesaGestionRepository _mesas;
    private readonly IZonaRepository _zonas;
    private readonly IReservaRepository _reservas;
    private readonly IParametrosReservaService _parametros;
    private readonly IValidator<RegistrarVisitaRequest> _validador;
    private readonly IValidator<ListarVisitasRequest> _validadorListado;
    private readonly IPublicadorEventosSeguro _publicador;
    private readonly TimeProvider _reloj;

    /// <summary>Crea el servicio con sus repositorios, los validadores, el publicador y el reloj.</summary>
    public VisitaService(
        IVisitaRepository visitas,
        IFranjaGestionRepository franjas,
        IMesaGestionRepository mesas,
        IZonaRepository zonas,
        IReservaRepository reservas,
        IParametrosReservaService parametros,
        IValidator<RegistrarVisitaRequest> validador,
        IValidator<ListarVisitasRequest> validadorListado,
        IPublicadorEventosSeguro publicador,
        TimeProvider reloj)
    {
        _parametros = parametros;
        _visitas = visitas;
        _franjas = franjas;
        _mesas = mesas;
        _zonas = zonas;
        _reservas = reservas;
        _validador = validador;
        _validadorListado = validadorListado;
        _publicador = publicador;
        _reloj = reloj;
    }

    /// <inheritdoc />
    public async Task<OperationResult<VisitasDelDiaDto>> ListarAsync(
        ListarVisitasRequest request, CancellationToken cancellationToken)
    {
        var validacion = await _validadorListado.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<VisitasDelDiaDto>(ErrorType.Validation, validacion.ToString());
        }

        // Sin fecha, hoy en Santo Domingo (el mismo "hoy" con el que el POST guarda la visita). El validador ya
        // comprobó que, si viene, es un aaaa-mm-dd válido.
        var fecha = string.IsNullOrWhiteSpace(request.Fecha)
            ? _reloj.HoyLocal()
            : DateOnly.ParseExact(request.Fecha.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture);
        var dia = await _visitas.ListarPorFechaAsync(fecha, TopeListado, cancellationToken);

        var visitas = dia.Filas
            .Select(f => new VisitaListadoDto(f.Id, f.FranjaNombre ?? "—", f.MesaNumero, f.Personas, HoraLocal(f.CreadaEnUtc)))
            .ToList();

        return OperationResult.Success(new VisitasDelDiaDto(
            fecha, dia.TotalVisitas, dia.TotalPersonas, Truncada: dia.TotalVisitas > visitas.Count, visitas));
    }

    // Hora local de Santo Domingo del momento UTC de registro. Se filtra por Fecha (ya local) y solo la hora se
    // convierte, así una visita registrada a las 21:00 locales (01:00 UTC del día siguiente) no cambia de día.
    private static TimeOnly? HoraLocal(DateTime creadaEnUtc) =>
        creadaEnUtc == default
            ? null
            : TimeOnly.FromDateTime(
                TimeZoneInfo.ConvertTimeFromUtc(DateTime.SpecifyKind(creadaEnUtc, DateTimeKind.Utc), HoraLocalGira.ZonaHoraria));

    /// <inheritdoc />
    public async Task<OperationResult<VisitaDto>> RegistrarAsync(
        RegistrarVisitaRequest request, CancellationToken cancellationToken)
    {
        var validacion = await _validador.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<VisitaDto>(ErrorType.Validation, validacion.ToString());
        }

        // Solo hoy (hora del servidor, America/Santo_Domingo).
        var ahoraLocal = _reloj.AhoraLocal();
        var hoy = DateOnly.FromDateTime(ahoraLocal);
        var horaActual = TimeOnly.FromDateTime(ahoraLocal);

        // Regla 1: el restaurante debe estar abierto AHORA. Una visita es un grupo que llega y se sienta de
        // inmediato, así que fuera del horario de operación (día o fecha cerrados, antes de abrir o después de
        // cerrar) no se registra. La apertura es inclusiva y el cierre exclusivo, igual que el fin de una franja
        // (el fin exacto cuenta como terminada).
        var parametros = await _parametros.ObtenerAsync(cancellationToken);
        if (parametros.HorarioDelDia(hoy) is not { } horario)
        {
            return OperationResult.Failure<VisitaDto>(ErrorType.Conflict, MensajesReservas.VisitaRestauranteCerradoHoy);
        }

        if (horaActual < horario.HoraApertura || horaActual >= horario.HoraCierre)
        {
            return OperationResult.Failure<VisitaDto>(
                ErrorType.Conflict,
                MensajesReservas.VisitaRestauranteCerradoAhora(horario.HoraApertura, horario.HoraCierre));
        }

        var franja = await _franjas.BuscarPorIdAsync(request.FranjaHorariaId, cancellationToken);
        if (franja is null)
        {
            return OperationResult.Failure<VisitaDto>(ErrorType.Validation, "La franja indicada no existe.");
        }

        if (!franja.Activa)
        {
            return OperationResult.Failure<VisitaDto>(ErrorType.Conflict, "La franja indicada no está activa.");
        }

        // La franja debe haber empezado.
        if (horaActual < franja.HoraInicio)
        {
            var inicio = franja.HoraInicio.ToString("HH:mm", CultureInfo.InvariantCulture);
            return OperationResult.Failure<VisitaDto>(
                ErrorType.Conflict,
                $"La franja {franja.Nombre} empieza a las {inicio}; todavía no se puede registrar una visita en ella.");
        }

        // Regla 2: la visita se registra en la franja MÁS RECIENTE de las activas que ya empezaron: la actual o,
        // si el cliente llega entre franjas (por ejemplo, a las 16:30), la que ya terminó pero fue la última en
        // empezar. Se registra contra esa franja anterior, no contra la próxima: el grupo ya está sentado ahora,
        // no cuando empiece la siguiente franja.
        var activas = (await _franjas.ListarTodasAsync(cancellationToken)).Where(f => f.Activa);
        var masReciente = activas
            .Where(f => f.HoraInicio <= horaActual)
            .OrderByDescending(f => f.HoraInicio)
            .FirstOrDefault();
        if (masReciente is not null && masReciente.Id != franja.Id)
        {
            return OperationResult.Failure<VisitaDto>(
                ErrorType.Conflict,
                MensajesReservas.VisitaFranjaNoVigente(masReciente.Nombre, masReciente.HoraInicio, masReciente.HoraFin));
        }

        // La mesa es obligatoria (decisión del líder): el validador ya rechazó MesaId null o vacío, así que
        // aquí siempre hay un id que resolver. Ya no hay rama "sin mesa".
        var mesa = await _mesas.BuscarPorIdAsync(request.MesaId!.Value, cancellationToken);
        if (mesa is null)
        {
            return OperationResult.Failure<VisitaDto>(ErrorType.Validation, "La mesa indicada no existe.");
        }

        if (!mesa.Activa)
        {
            return OperationResult.Failure<VisitaDto>(ErrorType.Conflict, $"La mesa {mesa.Numero} no está activa.");
        }

        // Si esto fuera null sería una violación de integridad: una mesa siempre tiene zona.
        var zona = await _zonas.BuscarPorIdAsync(mesa.ZonaId, cancellationToken)
            ?? throw new InvalidOperationException($"La mesa {mesa.Numero} referencia una zona que no existe.");
        if (!zona.Activa)
        {
            return OperationResult.Failure<VisitaDto>(
                ErrorType.Conflict, $"La zona de la mesa {mesa.Numero} no está activa.");
        }

        // Solo se compara con reservas vigentes: las visitas sin reserva no tienen hora de salida,
        // así que no se sabe si la mesa ya se liberó.
        if (await _reservas.ExisteVigenteAsync(mesa.Id, hoy, franja.Id, cancellationToken))
        {
            return OperationResult.Failure<VisitaDto>(
                ErrorType.Conflict, $"La mesa {mesa.Numero} tiene una reserva en esta franja.");
        }

        // Decisión del líder: una mesa admite un solo grupo por fecha y franja, con o sin reserva. Reutiliza
        // la misma consulta que ya usan el mapa (OcupacionService) y ReservaService; no se duplica lógica.
        // Sin índice único en BD: dos registros simultáneos en la misma mesa y franja podrían pasar esta
        // comprobación a la vez (carrera aceptada; es una acción manual del personal, no un flujo automático
        // de alto volumen).
        var mesasConVisita = await _visitas.ListarMesasConVisitaAsync(hoy, franja.Id, cancellationToken);
        if (mesasConVisita.Contains(mesa.Id))
        {
            return OperationResult.Failure<VisitaDto>(ErrorType.Conflict, MensajesReservas.MesaYaTieneVisita(mesa.Numero));
        }

        if (request.Personas > mesa.Capacidad)
        {
            return OperationResult.Failure<VisitaDto>(
                ErrorType.Validation, $"La mesa {mesa.Numero} admite como máximo {mesa.Capacidad} personas.");
        }

        var visita = new VisitaSinReserva
        {
            Id = Guid.NewGuid(),
            Fecha = hoy,
            FranjaHorariaId = franja.Id,
            MesaId = mesa.Id,
            PersonasReales = request.Personas
        };
        _visitas.Agregar(visita);
        await _visitas.GuardarCambiosAsync(cancellationToken);

        // Tras guardar, una sola vez; un handler que falla se registra y no revierte (SRS 3.3, regla 3).
        await _publicador.PublicarAsync(
            new VisitaCompletadaEvent(visita.Fecha, visita.FranjaHorariaId, visita.PersonasReales, ConReserva: false),
            $"VisitaId={visita.Id}",
            cancellationToken);

        return OperationResult.Success(new VisitaDto(
            visita.Id, visita.Fecha, franja.Id, franja.Nombre, visita.MesaId, mesa.Numero, visita.PersonasReales));
    }
}
