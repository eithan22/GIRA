using GIRA.Infrastructure.Identity;
using GIRA.Application.EventHandlers.Analitica;
using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Application.Interfaces.Services.Analitica;
using GIRA.Application.Services.Analitica;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.ExternalServices.Analitica;
using GIRA.Infrastructure.Persistence.Repositories.Analitica;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MediatR;

namespace GIRA.Infrastructure;

/// <summary>
/// Punto único de registro de los servicios de GIRA.Infrastructure
/// (persistencia y ASP.NET Core Identity) en el contenedor de dependencias de la API.
/// </summary>
public static class DependencyInjection
{
    /// <summary>
    /// Registra <see cref="GiraDbContext"/> contra SQL Server y configura ASP.NET Core
    /// Identity con las políticas de contraseña y bloqueo del proyecto.
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

        services.AddMediatR(mediatrConfiguration =>
            mediatrConfiguration.RegisterServicesFromAssembly(typeof(RegistrarAfluenciaAlConfirmarReservaHandler).Assembly));
        services.AddScoped<IAnaliticaRepository, AnaliticaRepository>();
        services.AddScoped<IAnaliticaService, AnaliticaService>();
        services.AddScoped<IAfluenciaPredictionService, AfluenciaPredictionService>();
        services.AddHostedService<PrediccionAfluenciaBackgroundService>();

        return services;
    }
}
