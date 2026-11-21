using MediatR;

namespace GIRA.Application.Events.Reservas;

/// <summary>
/// Se publica cuando el personal modifica una reserva existente (el cliente no puede modificar:
/// cancela y reserva de nuevo). Según el SRS 3.4 lleva la fecha y la franja anteriores y las nuevas,
/// para que Inventario recalcule ambos días, y la cantidad de personas actual. Solo datos mínimos.
/// </summary>
public sealed record ReservaModificadaEvent(
    Guid ReservaId,
    DateOnly FechaAnterior,
    Guid FranjaHorariaIdAnterior,
    DateOnly FechaNueva,
    Guid FranjaHorariaIdNueva,
    int Personas) : INotification;
