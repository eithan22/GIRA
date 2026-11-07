using GIRA.Application.Interfaces.Repository.Reservas;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Infrastructure.Persistence.Repositories.Reservas;

/// <summary>Registra los repositorios del módulo de Reservas.</summary>
public static class ReservasRepositoriesExtensions
{
    /// <summary>Registra los repositorios de Mesa, FranjaHoraria y Reserva.</summary>
    public static IServiceCollection AddReservasRepositories(this IServiceCollection services)
    {
        services.AddScoped<IMesaRepository, MesaRepository>();
        services.AddScoped<IFranjaHorariaRepository, FranjaHorariaRepository>();
        services.AddScoped<IReservaRepository, ReservaRepository>();
        return services;
    }
}
