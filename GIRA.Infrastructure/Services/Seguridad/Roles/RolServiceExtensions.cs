using GIRA.Application.Interfaces.Services.Seguridad.Roles;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Infrastructure.Services.Seguridad.Roles;

public static class RolServiceExtensions
{
    public static IServiceCollection AddRolService(this IServiceCollection services)
    {
        services.AddScoped<IRolService, RolService>();
        return services;
    }
}
