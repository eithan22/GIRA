using GIRA.Application.Common;
using GIRA.Application.Interfaces.Services.Analitica;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GIRA.Infrastructure.Services.Analitica;

public sealed class CapacidadOperativaBackgroundService(
    IServiceScopeFactory scopes,
    TimeProvider reloj,
    ILogger<CapacidadOperativaBackgroundService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1), reloj);
        do
        {
            try
            {
                var ahora = reloj.AhoraLocal();
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<ICapacidadOperativaService>()
                    .CapturarAntesDeAperturaAsync(
                        DateOnly.FromDateTime(ahora), TimeOnly.FromDateTime(ahora), stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "No se pudo capturar la capacidad operativa diaria.");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
