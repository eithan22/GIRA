using GIRA.Application.Interfaces.Services.Configuracion;
using GIRA.Application.Interfaces.Services.Reservas;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GIRA.Application.Services.Configuracion;

/// <summary>Registra los servicios del módulo Configuración.</summary>
public static class ConfiguracionServiceExtensions
{
    /// <summary>
    /// Registra <see cref="IParametrosReservaService"/> (Scoped). La caché (<c>IMemoryCache</c>) la registra
    /// Infrastructure con <c>AddMemoryCache()</c>, porque Application solo conoce las abstracciones.
    /// </summary>
    public static IServiceCollection AddConfiguracion(this IServiceCollection services)
    {
        services.AddScoped<IParametrosReservaService, ParametrosReservaDbService>();
        services.AddScoped<IConfiguracionReservasService, ConfiguracionReservasService>();
        services.AddScoped<IParametrosInventarioService, ParametrosInventarioService>();
        services.AddScoped<INotificacionService, NotificacionService>();
        services.TryAddSingleton(TimeProvider.System);
        return services;
    }
}
