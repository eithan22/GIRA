using GIRA.Application.Interfaces.Services.Configuracion;
using GIRA.Application.Interfaces.Services.Reservas;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Application.Services.Reservas;

/// <summary>Registra los servicios del módulo de Reservas.</summary>
public static class ReservasServiceExtensions
{
    /// <summary>Registra los servicios de Reservas (los parámetros los registra <c>AddConfiguracion</c>).</summary>
    public static IServiceCollection AddReservas(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddScoped<IDisponibilidadService, DisponibilidadService>();
        services.AddScoped<IPoliticasPublicasService, PoliticasPublicasService>();
        services.AddScoped<IConsultaSalon, ConsultaSalonService>();
        services.AddSingleton<GeneradorCodigoReserva>();
        services.AddScoped<IReservaService, ReservaService>();
        services.AddScoped<IClienteService, ClienteService>();
        services.AddScoped<IVisitaService, VisitaService>();
        services.AddScoped<IOcupacionService, OcupacionService>();
        services.AddScoped<IProcesoAutomaticoReservasService, ProcesoAutomaticoReservasService>();
        services.AddScoped<IZonaService, ZonaService>();
        services.AddScoped<IMesaService, MesaService>();
        services.AddScoped<IFranjaService, FranjaService>();
        return services;
    }
}
