using MediatR;

namespace GIRA.Application.Events.Reservas;

/// <summary>
/// Se publica cuando una reserva se marca NoAsistio porque el cliente no llegó dentro
/// de la tolerancia configurada.
/// </summary>
public sealed record ReservaNoAsistidaEvent(Guid ReservaId) : INotification;
