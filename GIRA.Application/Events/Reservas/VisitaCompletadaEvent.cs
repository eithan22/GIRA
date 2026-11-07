using MediatR;

namespace GIRA.Application.Events.Reservas;

/// <summary>
/// Se publica cuando una visita termina, con o sin reserva previa (<see cref="ConReserva"/>
/// distingue ambos casos). Lo usa Analítica para medir afluencia.
/// </summary>
public sealed record VisitaCompletadaEvent(
    DateOnly Fecha,
    Guid FranjaHorariaId,
    int PersonasReales,
    bool ConReserva) : INotification;
