using GIRA.Application.Interfaces.Services.Personal;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Application.Services.Personal;

/// <summary>Registra los servicios del módulo de Personal y Turnos.</summary>
public static class PersonalServiceExtensions
{
    /// <summary>
    /// Punto único de registro del módulo de Personal y Turnos. Cada historia agrega aquí
    /// sus servicios (cargos, empleados, disponibilidad, turnos, etc.) a medida que se implementa.
    /// </summary>
    public static IServiceCollection AddPersonal(this IServiceCollection services)
    {
        // Cargos (GI-296).
        services.AddScoped<ICargoService, CargoService>();

        // Empleados (GI-295).
        services.AddScoped<IEmpleadoService, EmpleadoService>();

        // Disponibilidad semanal (GI-297).
        services.AddScoped<IDisponibilidadService, DisponibilidadService>();

        // Turnos (GI-298).
        services.AddScoped<ITurnoService, TurnoService>();

        // Planificación semanal con cobertura (GI-301).
        services.AddScoped<IPlanificacionService, PlanificacionService>();

        // Ausencias (GI-305).
        services.AddScoped<IAusenciaService, AusenciaService>();

        // Requerimiento de personal por cargo (GI-302).
        services.AddScoped<IRequerimientoPersonalService, RequerimientoPersonalService>();

        return services;
    }
}
