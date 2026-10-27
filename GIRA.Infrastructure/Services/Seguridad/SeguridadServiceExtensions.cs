using GIRA.Application.Interfaces.Services.Seguridad.Accesos;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Infrastructure.Identity;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Infrastructure.Services.Seguridad;

/// <summary>Registra ASP.NET Core Identity y los servicios del módulo de Seguridad.</summary>
public static class SeguridadServiceExtensions
{
    /// <summary>
    /// Configura Identity con las políticas de contraseña y bloqueo del proyecto y registra
    /// los servicios de autenticación, acceso y usuarios del módulo de Seguridad.
    /// </summary>
    public static IServiceCollection AddSeguridad(this IServiceCollection services, IConfiguration configuration)
    {
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
            .AddSignInManager()
            .AddErrorDescriber<IdentityErrorDescriberEspanol>();

        // Tokens de sesión e inicio de sesión (GI-95).
        services.AddSingleton(JwtSettings.Leer(configuration));
        services.AddScoped<ITokenService, TokenService>();
        services.AddScoped<IRotadorRefreshToken, RotadorRefreshToken>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IRegistroAccesoService, RegistroAccesoService>();
        services.AddScoped<IHistorialAccesosService, HistorialAccesosService>();

        services.AddUsuarioService();

        return services;
    }
}
