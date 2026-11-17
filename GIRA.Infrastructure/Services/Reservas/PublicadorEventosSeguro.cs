using GIRA.Application.Interfaces.Services.Reservas;
using MediatR;
using Microsoft.Extensions.Logging;

namespace GIRA.Infrastructure.Services.Reservas;

/// <summary>
/// Implementación de <see cref="IPublicadorEventosSeguro"/> sobre <see cref="IPublisher"/> de MediatR
/// y <see cref="ILogger"/>. Cuando se instale Serilog como proveedor de logging, este helper lo
/// usa sin cambios.
/// </summary>
public class PublicadorEventosSeguro : IPublicadorEventosSeguro
{
    private readonly IPublisher _publisher;
    private readonly ILogger<PublicadorEventosSeguro> _logger;

    /// <summary>Crea el publicador con el publicador de MediatR y el logger.</summary>
    public PublicadorEventosSeguro(IPublisher publisher, ILogger<PublicadorEventosSeguro> logger)
    {
        _publisher = publisher;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task PublicarAsync<TEvento>(TEvento evento, string referencia, CancellationToken cancellationToken)
        where TEvento : INotification
    {
        try
        {
            await _publisher.Publish(evento, cancellationToken);
        }
        catch (Exception ex)
        {
            // La operación ya está guardada: no se revierte ni se propaga (SRS 3.3, regla 3).
            _logger.LogError(
                ex,
                "Falló un handler al publicar el evento {Evento} ({Referencia}); la operación ya estaba confirmada y no se revierte.",
                typeof(TEvento).Name,
                referencia);
        }
    }
}
