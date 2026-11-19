using GIRA.Application.Events.Analitica;
using GIRA.Application.Interfaces.Services.Inventario;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Application.EventHandlers.Inventario;

/// <summary>
/// Cuando Analítica publica una predicción de afluencia, guarda los comensales esperados por fecha y recalcula
/// la proyección de esas fechas con el mayor valor entre reservas y predicción (SRS 5.3, GI-455).
/// </summary>
public class ProyeccionPorPrediccionHandler : INotificationHandler<PrediccionAfluenciaGeneradaEvent>
{
    private readonly IServiceScopeFactory _scopes;

    /// <summary>Crea el handler.</summary>
    public ProyeccionPorPrediccionHandler(IServiceScopeFactory scopes)
    {
        _scopes = scopes;
    }

    /// <inheritdoc />
    public async Task Handle(PrediccionAfluenciaGeneradaEvent notification, CancellationToken cancellationToken)
    {
        // Scope propio: si Inventario falla, no deja cambios a medias en el contexto de Analítica.
        using var scope = _scopes.CreateScope();
        var proyeccion = scope.ServiceProvider.GetRequiredService<IProyeccionConsumoService>();
        await proyeccion.AplicarPrediccionAsync(notification, cancellationToken);
    }
}
