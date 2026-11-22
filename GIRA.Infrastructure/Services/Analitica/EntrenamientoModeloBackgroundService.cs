using GIRA.Application.Interfaces.Services.Analitica;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GIRA.Infrastructure.Services.Analitica;

public sealed class EntrenamientoModeloBackgroundService(
    IServiceScopeFactory scopes,
    TimeProvider reloj,
    ILogger<EntrenamientoModeloBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = scopes.CreateScope();
                var procesado = await scope.ServiceProvider
                    .GetRequiredService<IEntrenamientoModeloService>()
                    .ProcesarSiguienteAsync(stoppingToken);
                if (procesado)
                    continue;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falló el procesamiento de una solicitud de entrenamiento de Analítica.");
            }

            try
            {
                await Task.Delay(TimeSpan.FromSeconds(5), reloj, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
        }
    }
}
