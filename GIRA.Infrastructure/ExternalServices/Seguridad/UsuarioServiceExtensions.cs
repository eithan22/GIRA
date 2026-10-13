using GIRA.Application.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Infrastructure.ExternalServices.Seguridad;

public static class UsuarioServiceExtensions
{
    public static IServiceCollection AddUsuarioService(this IServiceCollection services)
    {
        services.AddScoped<IUsuarioService, UsuarioService>();
        return services;
    }
}