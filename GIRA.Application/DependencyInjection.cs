using FluentValidation;
using GIRA.Application.Interfaces.Services.Analitica;
using GIRA.Application.Interfaces.Services.Personal;
using GIRA.Application.Services.Analitica;
using GIRA.Application.Services.Configuracion;
using GIRA.Application.Services.Inventario;
using GIRA.Application.Services.Personal;
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
        services.AddInventario();
        services.AddScoped<IHistorialAfluenciaService, HistorialAfluenciaService>();
        services.AddScoped<IKpisService, KpisService>();
        services.AddScoped<IPrediccionAfluenciaService, PrediccionAfluenciaService>();
        services.AddScoped<IGeneracionPrediccionService, GeneracionPrediccionService>();
        services.AddScoped<IEntrenamientoModeloService, EntrenamientoModeloService>();
        services.AddScoped<ICapacidadOperativaService, CapacidadOperativaService>();
        services.AddScoped<IMarcacionTurnoService, MarcacionTurnoService>();
        services.AddScoped<IRecomendacionesService, RecomendacionesService>();
        return services;
    }
}
