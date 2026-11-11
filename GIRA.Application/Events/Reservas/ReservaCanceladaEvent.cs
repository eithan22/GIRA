using MediatR;

namespace GIRA.Application.Events.Reservas;

/// <summary>
/// Se publica cuando una reserva se cancela (SRS 3.4); Inventario recalcula por fecha y franja.
/// Lleva solo datos mínimos: el motivo es texto libre y puede traer datos personales, así que
/// no viaja en el evento (se guarda en la propia reserva).
/// </summary>
public sealed record ReservaCanceladaEvent(
    Guid ReservaId,
    DateOnly Fecha,
    Guid FranjaHorariaId,
    int Personas) : INotification;
