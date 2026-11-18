using GIRA.Application.Events.Reservas;
using GIRA.Application.Interfaces.Services.Inventario;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Application.EventHandlers.Inventario;

/// <summary>Al completarse una visita, descuenta del stock lo que comieron según las recetas (RF-INV-08).</summary>
public class SalidaTeoricaPorVisitaHandler : INotificationHandler<VisitaCompletadaEvent>
{
    private readonly IServiceScopeFactory _scopes;

    /// <summary>Crea el handler.</summary>
    public SalidaTeoricaPorVisitaHandler(IServiceScopeFactory scopes)
    {
        _scopes = scopes;
    }

    /// <inheritdoc />
    public async Task Handle(VisitaCompletadaEvent notification, CancellationToken cancellationToken)
    {
        // shortcut: el evento no trae VisitaId, así que no se puede evitar descontar dos veces si se publicara
        // dos veces; Reservas lo publica una sola vez (RF-RES-18). Pedir VisitaId a Reservas si eso cambia (GI-462).
        // Scope propio: si Inventario falla, no deja cambios a medias en el contexto de Reservas.
        using var scope = _scopes.CreateScope();
        var movimientos = scope.ServiceProvider.GetRequiredService<IMovimientoInventarioService>();
        await movimientos.RegistrarSalidasTeoricasAsync(
            notification.Fecha, notification.PersonasReales, notification.ConReserva, cancellationToken);
    }
}
