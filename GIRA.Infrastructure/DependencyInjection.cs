using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Interceptors;
using GIRA.Infrastructure.Persistence.Repositories.Configuracion;
using GIRA.Infrastructure.Persistence.Repositories.Reservas;
using GIRA.Infrastructure.Services.Reservas;
using GIRA.Infrastructure.Services.Seguridad;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;


namespace GIRA.Infrastructure;

/// <summary>
/// Punto único de registro de los servicios de GIRA.Infrastructure
/// (persistencia y ASP.NET Core Identity) en el contenedor de dependencias de la API.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra <see cref="GiraDbContext"/> contra SQL Server, configura ASP.NET Core
    /// Identity con las políticas de contraseña y bloqueo del proyecto y registra los
    /// servicios de autenticación.
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("GiraDb")
            ?? throw new InvalidOperationException(
                "Falta la cadena de conexión 'GiraDb'. Configúrala en User Secrets con la clave 'ConnectionStrings:GiraDb'.");

        services.AddHttpContextAccessor();
        services.AddScoped<AuditoriaSaveChangesInterceptor>();

        services.AddDbContext<GiraDbContext>((sp, options) =>
            options.UseSqlServer(connectionString)
                .AddInterceptors(sp.GetRequiredService<AuditoriaSaveChangesInterceptor>()));

        services.AddSeguridad(configuration);
        // Caché en memoria que usa ParametrosReservaDbService (Application solo conoce IMemoryCache).
        services.AddMemoryCache();
        services.AddConfiguracionRepositories();
        services.AddReservasRepositories();
        services.AddReservasServicios(configuration);

        return services;
    }
}
