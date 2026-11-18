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

        return services;
    }
}
