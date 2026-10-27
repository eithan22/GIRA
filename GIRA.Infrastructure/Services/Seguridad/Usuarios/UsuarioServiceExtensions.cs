using GIRA.Application.Interfaces.Services.Seguridad.Roles;
using GIRA.Application.Interfaces.Services.Seguridad.Usuarios;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Infrastructure.Services.Seguridad;

public static class UsuarioServiceExtensions
{
    public static IServiceCollection AddUsuarioService(this IServiceCollection services)
    {
        services.AddScoped<IUsuarioService, UsuarioService>();
        services.AddScoped<IRolService, RolService>();
        return services;
    }
}