using GIRA.Application.Events.Reservas;
using GIRA.Application.Interfaces.Services.Inventario;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Application.EventHandlers.Inventario;

/// <summary>
/// Cuando una reserva se confirma, se modifica, se cancela o no se presenta, recalcula el consumo
/// proyectado de esa fecha y revisa las alertas (RF-INV-07, RN-INV-06).
/// </summary>
public class ProyeccionPorReservaHandler :
    INotificationHandler<ReservaConfirmadaEvent>,
    INotificationHandler<ReservaModificadaEvent>,
    INotificationHandler<ReservaCanceladaEvent>,
    INotificationHandler<ReservaNoAsistidaEvent>
{
    private readonly IServiceScopeFactory _scopes;

    /// <summary>Crea el handler.</summary>
    public ProyeccionPorReservaHandler(IServiceScopeFactory scopes)
    {
        _scopes = scopes;
    }

    /// <inheritdoc />
    public Task Handle(ReservaConfirmadaEvent notification, CancellationToken cancellationToken) =>
        RecalcularAsync(notification.Fecha, notification.Fecha, cancellationToken);

    /// <inheritdoc />
    public Task Handle(ReservaModificadaEvent notification, CancellationToken cancellationToken)
    {
        var (a, b) = (notification.FechaAnterior, notification.FechaNueva);
        return RecalcularAsync(a < b ? a : b, a < b ? b : a, cancellationToken);
    }

    /// <inheritdoc />
    public Task Handle(ReservaCanceladaEvent notification, CancellationToken cancellationToken) =>
        RecalcularAsync(notification.Fecha, notification.Fecha, cancellationToken);

    /// <inheritdoc />
    public Task Handle(ReservaNoAsistidaEvent notification, CancellationToken cancellationToken) =>
        RecalcularAsync(notification.Fecha, notification.Fecha, cancellationToken);

    // Scope propio: si Inventario falla, no deja cambios a medias en el contexto de Reservas.
    private async Task RecalcularAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var proyeccion = scope.ServiceProvider.GetRequiredService<IProyeccionConsumoService>();
        await proyeccion.RecalcularAsync(desde, hasta, cancellationToken);
    }
}
