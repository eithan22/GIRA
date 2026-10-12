using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Application;

/// <summary>Registra los servicios de GIRA.Application.</summary>
public static class DependencyInjection
{
    /// <summary>Registra los validadores de FluentValidation.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        return services;
    }
}
