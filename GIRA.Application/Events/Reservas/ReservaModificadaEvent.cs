using MediatR;

namespace GIRA.Application.Events.Reservas;

/// <summary>
/// Se publica cuando el personal modifica una reserva existente (el cliente no puede
/// modificar: cancela y reserva de nuevo).
/// </summary>
public sealed record ReservaModificadaEvent(
    Guid ReservaId,
    Guid MesaId,
    Guid FranjaHorariaId,
    DateOnly Fecha,
    int Personas) : INotification;
