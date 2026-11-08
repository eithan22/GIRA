using GIRA.Application.Interfaces.Services.Reservas;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Application.Services.Reservas;

/// <summary>Registra los servicios del módulo de Reservas.</summary>
public static class ReservasServiceExtensions
{
    /// <summary>Registra <see cref="IParametrosReservaService"/> con sus valores por defecto.</summary>
    public static IServiceCollection AddReservas(this IServiceCollection services)
    {
        services.AddSingleton<IParametrosReservaService, ParametrosReservaService>();
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IDisponibilidadService, DisponibilidadService>();
        services.AddSingleton<GeneradorCodigoReserva>();
        services.AddScoped<IReservaService, ReservaService>();
        return services;
    }
}
