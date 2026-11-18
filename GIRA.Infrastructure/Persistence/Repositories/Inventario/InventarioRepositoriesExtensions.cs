using GIRA.Application.Interfaces.Repository.Inventario;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Infrastructure.Persistence.Repositories.Inventario;

/// <summary>Registra los repositorios del módulo de Inventario.</summary>
public static class InventarioRepositoriesExtensions
{
    /// <summary>Registra los repositorios de catálogos, insumos, platos, movimientos, alertas y consumo proyectado.</summary>
    public static IServiceCollection AddInventarioRepositories(this IServiceCollection services)
    {
        services.AddScoped<ICatalogoInventarioRepository, CatalogoInventarioRepository>();
        services.AddScoped<IInsumoRepository, InsumoRepository>();
        services.AddScoped<IPlatoRepository, PlatoRepository>();
        services.AddScoped<IMovimientoInventarioRepository, MovimientoInventarioRepository>();
        services.AddScoped<IAlertaInventarioRepository, AlertaInventarioRepository>();
        services.AddScoped<IConsumoProyectadoRepository, ConsumoProyectadoRepository>();
        return services;
    }
}
