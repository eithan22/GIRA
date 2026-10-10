using GIRA.Application.Interfaces.Services.Analitica;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GIRA.Infrastructure.ExternalServices.Analitica;

/// <summary>Actualiza los pronósticos al iniciar la API y cada 24 horas.</summary>
public sealed class PrediccionAfluenciaBackgroundService(
    IServiceScopeFactory scopeFactory,
    ILogger<PrediccionAfluenciaBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(24));

        do
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var predictionService = scope.ServiceProvider.GetRequiredService<IAfluenciaPredictionService>();
            logger.LogInformation("Iniciando actualización de predicciones de afluencia.");
            await predictionService.GenerarPrediccionesAsync(stoppingToken);
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
