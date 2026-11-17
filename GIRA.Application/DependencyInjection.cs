using FluentValidation;
using GIRA.Application.Services.Configuracion;
using GIRA.Application.Services.Reservas;
using MediatR;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Application;

/// <summary>Registra los servicios de GIRA.Application.</summary>
public static class DependencyInjection
{
    /// <summary>Registra los validadores de FluentValidation, MediatR y los servicios de los módulos.</summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(typeof(DependencyInjection).Assembly);
        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(DependencyInjection).Assembly));
        services.AddConfiguracion();
        services.AddReservas();
        return services;
    }
}
