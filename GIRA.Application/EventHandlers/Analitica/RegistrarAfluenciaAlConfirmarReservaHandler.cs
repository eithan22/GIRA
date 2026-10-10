using GIRA.Application.Events.Reservas;
using GIRA.Application.Interfaces.Services.Analitica;
using MediatR;

namespace GIRA.Application.EventHandlers.Analitica;

/// <summary>Registra la afluencia cada vez que Reservas publica una confirmación.</summary>
public sealed class RegistrarAfluenciaAlConfirmarReservaHandler(
    IAnaliticaService analiticaService) : INotificationHandler<ReservaConfirmadaEvent>
{
    /// <inheritdoc />
    public Task Handle(ReservaConfirmadaEvent notification, CancellationToken cancellationToken) =>
        analiticaService.RegistrarReservaConfirmadaAsync(
            notification.ReservaId,
            notification.Fecha,
            notification.HoraInicio,
            notification.CantidadComensales,
            cancellationToken);
}
