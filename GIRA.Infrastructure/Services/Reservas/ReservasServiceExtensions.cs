using GIRA.Application.Interfaces.Services.Reservas;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Infrastructure.Services.Reservas;

/// <summary>Registra los servicios del módulo de Reservas que viven en Infrastructure.</summary>
public static class ReservasServiceExtensions
{
    /// <summary>
    /// Registra <see cref="IPublicadorEventosSeguro"/> y el proceso automático de reservas
    /// (<see cref="ReservasProcesoAutomaticoService"/>), salvo que <c>Reservas:ProcesoAutomatico:Habilitado</c>
    /// sea false. Nunca lanza por una configuración inválida.
    /// </summary>
    public static IServiceCollection AddReservasServicios(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddScoped<IPublicadorEventosSeguro, PublicadorEventosSeguro>();

        var opciones = ProcesoAutomaticoReservasOptions.Desde(configuration);
        services.AddSingleton(opciones);
        if (opciones.Habilitado)
        {
            services.AddHostedService<ReservasProcesoAutomaticoService>();
        }

        return services;
    }
}
