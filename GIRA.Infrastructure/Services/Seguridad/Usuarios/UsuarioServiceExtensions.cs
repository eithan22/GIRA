using GIRA.Application.Interfaces.Services.Seguridad.Usuarios;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Infrastructure.Services.Seguridad.Usuarios;

public static class UsuarioServiceExtensions
{
    public static IServiceCollection AddUsuarioService(this IServiceCollection services)
    {
        services.AddScoped<IUsuarioService, UsuarioService>();
        return services;
    }
}