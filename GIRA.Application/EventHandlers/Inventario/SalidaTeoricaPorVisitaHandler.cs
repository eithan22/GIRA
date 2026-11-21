using GIRA.Application.Events.Reservas;
using GIRA.Application.Interfaces.Services.Inventario;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GIRA.Application.EventHandlers.Inventario;

/// <summary>
/// Al completarse una visita, descuenta del stock lo que comieron según las recetas (RF-INV-08). Una visita que
/// ya se descontó no se descuenta otra vez (GI-462).
/// </summary>
public class SalidaTeoricaPorVisitaHandler : INotificationHandler<VisitaCompletadaEvent>
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<SalidaTeoricaPorVisitaHandler> _logger;

    /// <summary>Crea el handler.</summary>
    public SalidaTeoricaPorVisitaHandler(IServiceScopeFactory scopes, ILogger<SalidaTeoricaPorVisitaHandler> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task Handle(VisitaCompletadaEvent notification, CancellationToken cancellationToken)
    {
        // Scope propio: si Inventario falla, no deja cambios a medias en el contexto de Reservas.
        using var scope = _scopes.CreateScope();
        var movimientos = scope.ServiceProvider.GetRequiredService<IMovimientoInventarioService>();
        var descontada = await movimientos.RegistrarSalidasTeoricasAsync(
            notification.Fecha, notification.PersonasReales, notification.ConReserva, notification.VisitaId, cancellationToken);

        if (!descontada)
        {
            _logger.LogWarning(
                "La visita {VisitaId} ya tenía salidas teóricas; el evento repetido se ignoró sin cambiar el stock.",
                notification.VisitaId);
        }
    }
}
