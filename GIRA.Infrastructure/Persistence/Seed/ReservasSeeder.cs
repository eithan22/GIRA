using GIRA.Domain.Entities.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Seed;

/// <summary>
/// Siembra zonas y mesas de ejemplo para poder probar el módulo Reservas en Development.
/// Es idempotente: no duplica datos si ya existen. No se ejecuta en otros ambientes.
/// </summary>
public static class ReservasSeeder
{
    /// <summary>Crea las zonas y mesas de ejemplo si todavía no existen.</summary>
    public static async Task SeedAsync(GiraDbContext contexto)
    {
        if (await contexto.Zonas.AnyAsync())
        {
            return;
        }

        var salonPrincipal = new Zona { Id = Guid.NewGuid(), Nombre = "Salón principal" };
        var terraza = new Zona { Id = Guid.NewGuid(), Nombre = "Terraza" };

        contexto.Zonas.AddRange(salonPrincipal, terraza);

        contexto.Mesas.AddRange(
            new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2, ZonaId = salonPrincipal.Id },
            new Mesa { Id = Guid.NewGuid(), Numero = 2, Capacidad = 4, ZonaId = salonPrincipal.Id },
            new Mesa { Id = Guid.NewGuid(), Numero = 3, Capacidad = 6, ZonaId = salonPrincipal.Id },
            new Mesa { Id = Guid.NewGuid(), Numero = 4, Capacidad = 4, ZonaId = terraza.Id },
            new Mesa { Id = Guid.NewGuid(), Numero = 5, Capacidad = 8, ZonaId = terraza.Id });

        await contexto.SaveChangesAsync();
    }
}
