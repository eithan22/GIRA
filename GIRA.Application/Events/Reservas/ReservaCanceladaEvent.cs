using MediatR;

namespace GIRA.Application.Events.Reservas;

/// <summary>
/// Se publica cuando una reserva se cancela. El motivo puede ser null (el cliente
/// cancela sin dar motivo; el personal está obligado a darlo).
/// </summary>
public sealed record ReservaCanceladaEvent(
    Guid ReservaId,
    string? MotivoCancelacion) : INotification;
