using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Application.Services.Reservas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace GIRA.Infrastructure.Services.Reservas;

/// <summary>
/// Único <see cref="BackgroundService"/> de Reservas (GI-161; GI-224 lo amplía sin duplicarlo): solo
/// orquesta. Espera unos segundos tras el arranque, y luego crea un scope por ciclo y le pide a
/// <see cref="IProcesoAutomaticoReservasService"/> que haga el trabajo. Usa el <see cref="TimeProvider"/>
/// inyectado, así que las pruebas avanzan un reloj falso en vez de esperar. Nunca lanza: un ciclo que
/// falla se registra y el siguiente se ejecuta.
/// </summary>
public sealed class ReservasProcesoAutomaticoService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ProcesoAutomaticoReservasOptions _opciones;
    private readonly TimeProvider _reloj;
    private readonly ILogger<ReservasProcesoAutomaticoService> _logger;

    /// <summary>Crea el servicio con la fábrica de scopes, las opciones, el reloj y el logger.</summary>
    public ReservasProcesoAutomaticoService(
        IServiceScopeFactory scopeFactory,
        ProcesoAutomaticoReservasOptions opciones,
        TimeProvider reloj,
        ILogger<ReservasProcesoAutomaticoService> logger)
    {
        _scopeFactory = scopeFactory;
        _opciones = opciones;
        _reloj = reloj;
        _logger = logger;
    }

    /// <inheritdoc />
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            foreach (var advertencia in _opciones.Advertencias)
            {
                _logger.LogWarning("Configuración del proceso automático de reservas: {Advertencia}", advertencia);
            }

            // El primer ciclo espera: así no compite con Migrate() y el seeder del arranque.
            if (_opciones.RetrasoInicialSegundos > 0)
            {
                await Task.Delay(TimeSpan.FromSeconds(_opciones.RetrasoInicialSegundos), _reloj, stoppingToken);
            }

            using var temporizador = new PeriodicTimer(TimeSpan.FromSeconds(_opciones.IntervaloSegundos), _reloj);
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
            _logger.LogError(ex, "El proceso automático de reservas se detuvo por un error inesperado.");
        }
    }

    /// <summary>
    /// Un ciclo: scope nuevo, ejecuta el proceso y registra el resultado (solo ids y conteos, sin datos
    /// personales). Un error del ciclo se registra y no se propaga; solo la cancelación sale.
    /// </summary>
    public async Task EjecutarCicloAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var proceso = scope.ServiceProvider.GetRequiredService<IProcesoAutomaticoReservasService>();

            var resultado = await proceso.EjecutarAsync(cancellationToken);

            if (resultado.NoAsistidas > 0 || resultado.Completadas > 0 || resultado.Omitidas > 0)
            {
                _logger.LogInformation(
                    "Proceso automático de reservas: {NoAsistidas} marcadas como no asistidas, {Completadas} completadas al terminar su franja, {Omitidas} omitidas porque otra ejecución ya las procesó.",
                    resultado.NoAsistidas, resultado.Completadas, resultado.Omitidas);
            }

            if (resultado.TopeAlcanzado)
            {
                _logger.LogWarning(
                    "Se alcanzó el tope de {Tope} reservas por ciclo del proceso automático; el resto se procesará en los ciclos siguientes.",
                    ProcesoAutomaticoReservasService.TopePorCiclo);
            }

            foreach (var fallo in resultado.Fallos)
            {
                _logger.LogError(
                    fallo.Excepcion,
                    "No se pudo procesar la reserva {ReservaId} en el proceso automático; se reintentará en el siguiente ciclo.",
                    fallo.ReservaId);
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falló un ciclo del proceso automático de reservas; se reintentará en el siguiente.");
        }
    }
}
