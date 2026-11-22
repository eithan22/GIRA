using GIRA.Application.Interfaces.Services.Analitica;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GIRA.Infrastructure.Services.Analitica;

/// <summary>Reintenta entregas pendientes y ejecuta la generación diaria tras el cierre.</summary>
public sealed class GeneracionPrediccionBackgroundService(
    IServiceScopeFactory scopes, TimeProvider reloj, ILogger<GeneracionPrediccionBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), reloj, stoppingToken);
            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5), reloj);
            do
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<IGeneracionPrediccionService>().EjecutarAsync(stoppingToken);
                }
                catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    logger.LogError(ex, "Falló la generación o entrega de predicciones; se reintentará en el siguiente ciclo.");
                }
            }
            while (await timer.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }
}
