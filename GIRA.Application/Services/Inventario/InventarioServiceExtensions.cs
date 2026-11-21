using GIRA.Application.Interfaces.Services.Inventario;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GIRA.Application.Services.Inventario;

/// <summary>Registra los servicios del módulo de Inventario.</summary>
public static class InventarioServiceExtensions
{
    /// <summary>Registra los servicios de Inventario (el puerto hacia Reservas lo registra <c>AddReservas</c>).</summary>
    public static IServiceCollection AddInventario(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<CalculadoraConsumo>();
        services.AddScoped<EvaluadorAlertasInventario>();
        services.AddScoped<ICatalogoInventarioService, CatalogoInventarioService>();
        services.AddScoped<IInsumoService, InsumoService>();
        services.AddScoped<IPlatoService, PlatoService>();
        services.AddScoped<IMovimientoInventarioService, MovimientoInventarioService>();
        services.AddScoped<IAlertaInventarioService, AlertaInventarioService>();
        services.AddScoped<IProyeccionConsumoService, ProyeccionConsumoService>();
        return services;
    }
}
