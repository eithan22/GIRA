using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Tests.Common;

/// <summary>
/// Siembra la configuración de reservas en una base de pruebas con el seeder real
/// (<see cref="ConfiguracionReservasSeeder"/>), subiendo el tamaño máximo de grupo para las pruebas que usan
/// mesas grandes (el valor por defecto, 8, también limita a las mesas más grandes).
/// </summary>
internal static class ConfiguracionDePrueba
{
    /// <summary>Siembra los valores por defecto y fija <c>MaxPersonasPorReserva</c> en <paramref name="maxPersonas"/>.</summary>
    public static async Task SembrarAsync(GiraDbContext contexto, int maxPersonas = 50)
    {
        await ConfiguracionReservasSeeder.SembrarAsync(contexto);
        var configuracion = await contexto.ConfiguracionesRestaurante.SingleAsync();
        configuracion.MaxPersonasPorReserva = maxPersonas;
        await contexto.SaveChangesAsync();
    }
}
