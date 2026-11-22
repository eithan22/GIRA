using GIRA.Application.Events.Analitica;
using GIRA.Application.Interfaces.Services.Personal;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Application.EventHandlers.Personal;

/// <summary>
/// Cuando Analítica publica una predicción de afluencia, Personal compara lo requerido con lo planificado y genera
/// sugerencias (GI-303, RF-PER-09). Usa un scope propio para no mezclar cambios con el contexto que publicó el evento.
/// Si falla, la excepción llega al publicador de eventos, que la registra.
/// </summary>
public class PrediccionAfluenciaPersonalHandler : INotificationHandler<PrediccionAfluenciaGeneradaEvent>
{
    private readonly IServiceScopeFactory _scopes;

    /// <summary>Crea el handler.</summary>
    public PrediccionAfluenciaPersonalHandler(IServiceScopeFactory scopes)
    {
        _scopes = scopes;
    }

    /// <inheritdoc />
    public async Task Handle(PrediccionAfluenciaGeneradaEvent notification, CancellationToken cancellationToken)
    {
        using var scope = _scopes.CreateScope();
        var generacion = scope.ServiceProvider.GetRequiredService<IGeneracionSugerenciasService>();
        await generacion.GenerarDesdePrediccionAsync(notification, cancellationToken);
    }
}
