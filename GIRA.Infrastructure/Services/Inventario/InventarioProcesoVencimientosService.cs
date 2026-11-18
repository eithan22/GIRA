using GIRA.Application.Interfaces.Services.Inventario;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GIRA.Infrastructure.Services.Inventario;

/// <summary>
/// Proceso diario de vencimientos (GI-483, RN-INV-11): una vez al día abre las alertas de Vencimiento de los lotes
/// que vencen dentro de los días configurados, sin duplicar una abierta. Igual que el proceso de Reservas: un
/// scope por ciclo, reloj inyectado y nunca lanza; un ciclo que falla queda en el log y el siguiente se ejecuta.
/// </summary>
public sealed class InventarioProcesoVencimientosService : BackgroundService
{
    /// <summary>Clave que lo apaga (las pruebas HTTP la ponen en false).</summary>
    public const string ClaveHabilitado = "Inventario:ProcesoVencimientos:Habilitado";

    /// <summary>Cada cuánto corre.</summary>
    public static readonly TimeSpan Intervalo = TimeSpan.FromDays(1);

    /// <summary>Espera tras el arranque, para no competir con Migrate() y los seeders.</summary>
    public static readonly TimeSpan RetrasoInicial = TimeSpan.FromSeconds(30);

    private readonly IServiceScopeFactory _scopes;
    private readonly TimeProvider _reloj;
    private readonly ILogger<InventarioProcesoVencimientosService> _logger;

    /// <summary>Crea el proceso.</summary>
    public InventarioProcesoVencimientosService(
        IServiceScopeFactory scopes, TimeProvider reloj, ILogger<InventarioProcesoVencimientosService> logger)
    {
        _scopes = scopes;
        _reloj = reloj;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(RetrasoInicial, _reloj, stoppingToken);
            using var temporizador = new PeriodicTimer(Intervalo, _reloj);
            do
            {
                await EjecutarCicloAsync(stoppingToken);
            }
            while (await temporizador.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Apagado normal de la aplicación.
        }
        catch (Exception ex)
        {
            // Una excepción aquí tumbaría el host: se registra y el proceso se detiene sin afectar a la API.
            _logger.LogError(ex, "El proceso diario de vencimientos de inventario se detuvo por un error inesperado.");
        }
    }

    /// <summary>Un ciclo: scope nuevo y revisión de vencimientos. Un error se registra y no se propaga; solo la cancelación sale.</summary>
    public async Task EjecutarCicloAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopes.CreateScope();
            var abiertas = await scope.ServiceProvider.GetRequiredService<IAlertaInventarioService>()
                .RevisarVencimientosAsync(cancellationToken);
            if (abiertas > 0)
            {
                _logger.LogInformation("Proceso de vencimientos de inventario: {Abiertas} alertas nuevas.", abiertas);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló la revisión diaria de vencimientos de inventario; se reintentará en el siguiente ciclo.");
        }
    }
}
