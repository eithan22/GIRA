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
    private readonly IVisitaRepository _visitas;
    private readonly IFranjaGestionRepository _franjas;
    private readonly IMesaGestionRepository _mesas;
    private readonly IZonaRepository _zonas;
    private readonly IReservaRepository _reservas;
    private readonly IValidator<RegistrarVisitaRequest> _validador;
    private readonly IPublicadorEventosSeguro _publicador;
    private readonly TimeProvider _reloj;

    /// <summary>Crea el servicio con sus repositorios, el validador, el publicador y el reloj.</summary>
    public VisitaService(
        IVisitaRepository visitas,
        IFranjaGestionRepository franjas,
        IMesaGestionRepository mesas,
        IZonaRepository zonas,
        IReservaRepository reservas,
        IValidator<RegistrarVisitaRequest> validador,
        IPublicadorEventosSeguro publicador,
        TimeProvider reloj)
    {
        _visitas = visitas;
        _franjas = franjas;
        _mesas = mesas;
        _zonas = zonas;
        _reservas = reservas;
        _validador = validador;
        _publicador = publicador;
        _reloj = reloj;
    }

    /// <inheritdoc />
    public async Task<OperationResult<VisitaDto>> RegistrarAsync(
        RegistrarVisitaRequest request, CancellationToken cancellationToken)
    {
        var validacion = await _validador.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<VisitaDto>(ErrorType.Validation, validacion.ToString());
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

        // Solo hoy (hora del servidor). La franja debe haber empezado; no se exige que siga vigente:
        // un cliente sin reserva puede llegar entre franjas (por ejemplo, a las 16:00).
        var ahoraLocal = _reloj.AhoraLocal();
        var hoy = DateOnly.FromDateTime(ahoraLocal);
        if (TimeOnly.FromDateTime(ahoraLocal) < franja.HoraInicio)
        {
            var inicio = franja.HoraInicio.ToString("HH:mm", CultureInfo.InvariantCulture);
            return OperationResult.Failure<VisitaDto>(
                ErrorType.Conflict,
                $"La franja {franja.Nombre} empieza a las {inicio}; todavía no se puede registrar una visita en ella.");
        }

        Mesa? mesa = null;
        if (request.MesaId is { } mesaId)
        {
            mesa = await _mesas.BuscarPorIdAsync(mesaId, cancellationToken);
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

            if (request.Personas > mesa.Capacidad)
            {
                return OperationResult.Failure<VisitaDto>(
                    ErrorType.Validation, $"La mesa {mesa.Numero} admite como máximo {mesa.Capacidad} personas.");
            }
        }

        var visita = new VisitaSinReserva
        {
            Id = Guid.NewGuid(),
            Fecha = hoy,
            FranjaHorariaId = franja.Id,
            MesaId = mesa?.Id,
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
            visita.Id, visita.Fecha, franja.Id, franja.Nombre, visita.MesaId, mesa?.Numero, visita.PersonasReales));
    }
}
