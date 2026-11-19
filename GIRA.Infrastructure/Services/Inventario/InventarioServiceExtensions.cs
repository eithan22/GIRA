using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Infrastructure.Services.Inventario;

/// <summary>Registra los servicios de Inventario que viven en Infrastructure.</summary>
public static class InventarioServiceExtensions
{
    /// <summary>Registra el proceso diario de vencimientos, salvo que <c>Inventario:ProcesoVencimientos:Habilitado</c> sea false.</summary>
    public static IServiceCollection AddInventarioServicios(this IServiceCollection services, IConfiguration configuration)
    {
        if (!bool.TryParse(configuration[InventarioProcesoVencimientosService.ClaveHabilitado], out var habilitado) || habilitado)
        {
            services.AddHostedService<InventarioProcesoVencimientosService>();
        }

        return services;
    }
}
