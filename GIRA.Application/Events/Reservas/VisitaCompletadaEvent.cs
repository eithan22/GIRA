using MediatR;

namespace GIRA.Application.Events.Reservas;

/// <summary>
/// Se publica cuando una visita termina, con o sin reserva previa (<see cref="ConReserva"/>
/// distingue ambos casos). Lo usa Analítica para medir afluencia e Inventario para descontar lo consumido.
/// </summary>
/// <param name="VisitaId">
/// Id de la reserva si <paramref name="ConReserva"/>; si no, el de la visita sin reserva. Con él Inventario no
/// descuenta dos veces la misma visita si el evento llegara repetido (GI-462).
/// </param>
public sealed record VisitaCompletadaEvent(
    DateOnly Fecha,
    Guid FranjaHorariaId,
    int PersonasReales,
    bool ConReserva,
    Guid VisitaId) : INotification;
