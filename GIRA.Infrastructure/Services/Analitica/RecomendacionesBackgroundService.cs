using GIRA.Application.Interfaces.Services.Analitica;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GIRA.Infrastructure.Services.Analitica;

/// <summary>Actualiza periódicamente las recomendaciones fuera de las solicitudes HTTP.</summary>
public sealed class RecomendacionesBackgroundService(
    IServiceScopeFactory scopes, TimeProvider reloj, ILogger<RecomendacionesBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(5), reloj);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<IRecomendacionesService>().ActualizarAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Falló la actualización de recomendaciones; se reintentará en el siguiente ciclo.");
            }
            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken)) return;
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
        } while (true);
    }
}
