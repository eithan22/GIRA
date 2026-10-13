using GIRA.Application.Interfaces.Services.Seguridad;
using GIRA.Application.Services.Seguridad;
using GIRA.Infrastructure.Identity;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Services.Seguridad;
using Microsoft.AspNetCore.Identity;
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

        services.AddDbContext<GiraDbContext>(options => options.UseSqlServer(connectionString));

        services.AddIdentityCore<Usuario>(options =>
        {
            options.Password.RequiredLength = 8;
            options.Password.RequireUppercase = true;
            options.Password.RequireLowercase = true;
            options.Password.RequireDigit = true;
            options.Password.RequireNonAlphanumeric = true;

            options.Lockout.MaxFailedAccessAttempts = 5;
            options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            options.Lockout.AllowedForNewUsers = true;

            options.User.RequireUniqueEmail = true;
        })
            .AddRoles<Rol>()
            .AddEntityFrameworkStores<GiraDbContext>()
            .AddSignInManager();

        // Tokens de sesión e inicio de sesión (GI-95).
        services.AddSingleton(JwtSettings.Leer(configuration));
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRegistroAccesoService, RegistroAccesoService>();
        services.AddScoped<IHistorialAccesosService, HistorialAccesosService>();

        return services;
    }
}
