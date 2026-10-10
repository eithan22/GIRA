using MediatR;

namespace GIRA.Application.Events.Reservas;

/// <summary>Contrato de integración publicado por Reservas al confirmar una reserva.</summary>
public sealed record ReservaConfirmadaEvent(
    Guid ReservaId,
    DateOnly Fecha,
    TimeOnly HoraInicio,
    int CantidadComensales) : INotification;
