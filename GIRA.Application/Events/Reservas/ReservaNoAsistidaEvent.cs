using MediatR;

namespace GIRA.Application.Events.Reservas;

/// <summary>
/// Se publica cuando una reserva se marca NoAsistio porque el cliente no llegó dentro de la tolerancia
/// configurada. Según el SRS 3.4 lleva la fecha, la franja y la cantidad de personas reservadas
/// (<c>Reserva.Personas</c>, no las reales: no hubo check-in); Inventario y Analítica recalculan con eso.
/// </summary>
public sealed record ReservaNoAsistidaEvent(
    Guid ReservaId,
    DateOnly Fecha,
    Guid FranjaHorariaId,
    int Personas) : INotification;
