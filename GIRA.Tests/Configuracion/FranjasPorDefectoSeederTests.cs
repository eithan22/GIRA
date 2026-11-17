using GIRA.Application.Services.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Tests.Configuracion;

/// <summary>
/// Franjas de 2 horas por defecto (GI-226): el seeder las crea solo si la tabla está vacía y nunca modifica
/// una franja existente. Base en memoria (no toca ninguna base real).
/// </summary>
public class FranjasPorDefectoSeederTests
{
    private static GiraDbContext CrearContexto() =>
        new(new DbContextOptionsBuilder<GiraDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);

    [Fact]
    public async Task SembrarAsync_ConLaTablaVacia_CreaLasCuatroFranjasConSusIdsYHoras()
    {
        await using var db = CrearContexto();

        await FranjasPorDefectoSeeder.SembrarAsync(db);

        var franjas = await db.FranjasHorarias.OrderBy(f => f.HoraInicio).ToListAsync();
        Assert.Equal(
            [
                ("Almuerzo", "12:00", "13:45", "00000000-0000-0000-0000-000000000001"),
                ("Tarde", "14:00", "15:45", "00000000-0000-0000-0000-000000000003"),
                ("Cena", "18:30", "20:15", "00000000-0000-0000-0000-000000000002"),
                ("Noche", "20:30", "22:15", "00000000-0000-0000-0000-000000000004")
            ],
            franjas.Select(f => (f.Nombre, f.HoraInicio.ToString("HH:mm"), f.HoraFin.ToString("HH:mm"), f.Id.ToString())).ToArray());
        Assert.All(franjas, f => Assert.True(f.Activa));
    }

    [Fact]
    public void LasFranjasPorDefecto_NoSeSolapan_TienenHuecoDe15MinSiSeTocan_YCabenEnElHorarioPorDefecto()
    {
        var franjas = FranjasPorDefecto.Todas.OrderBy(f => f.HoraInicio).ToList();
        var apertura = ParametrosReservaService.ValoresPorDefecto.HoraApertura;
        var cierre = ParametrosReservaService.ValoresPorDefecto.HoraCierre;

        Assert.All(franjas, f =>
        {
            Assert.True(f.HoraFin > f.HoraInicio);
            Assert.True(f.HoraInicio >= apertura && f.HoraFin <= cierre, f.Nombre);
        });
        for (var i = 1; i < franjas.Count; i++)
        {
            // No se solapan ni siquiera se tocan: entre una y la siguiente hay al menos 15 minutos.
            Assert.True(franjas[i].HoraInicio - franjas[i - 1].HoraFin >= TimeSpan.FromMinutes(15), franjas[i].Nombre);
        }
    }

    [Fact]
    public async Task SembrarAsync_ConFranjasExistentes_NoLasModificaNiAgregaNada()
    {
        await using var db = CrearContexto();
        var editada = new FranjaHoraria
        {
            Id = Guid.Parse("00000000-0000-0000-0000-000000000001"), Nombre = "Almuerzo largo",
            HoraInicio = new TimeOnly(11, 0), HoraFin = new TimeOnly(16, 0), Activa = false
        };
        db.FranjasHorarias.Add(editada);
        await db.SaveChangesAsync();

        await FranjasPorDefectoSeeder.SembrarAsync(db);

        var franja = await db.FranjasHorarias.SingleAsync();
        Assert.Equal("Almuerzo largo", franja.Nombre);
        Assert.Equal(new TimeOnly(11, 0), franja.HoraInicio);
        Assert.Equal(new TimeOnly(16, 0), franja.HoraFin);
        Assert.False(franja.Activa);
    }

    [Fact]
    public async Task SembrarAsync_ConUnaFranjaBorradaLogicamente_TambienCuentaComoExistente()
    {
        await using var db = CrearContexto();
        db.FranjasHorarias.Add(new FranjaHoraria
        {
            Id = Guid.NewGuid(), Nombre = "Vieja", HoraInicio = new TimeOnly(9, 0), HoraFin = new TimeOnly(10, 0), IsDeleted = true
        });
        await db.SaveChangesAsync();

        await FranjasPorDefectoSeeder.SembrarAsync(db);

        Assert.Empty(await db.FranjasHorarias.ToListAsync()); // la borrada no se ve; no se agregó nada
        Assert.Equal(1, await db.FranjasHorarias.IgnoreQueryFilters().CountAsync());
    }

    [Fact]
    public async Task SembrarAsync_SeEjecutaDosVeces_NoDuplicaNada()
    {
        await using var db = CrearContexto();

        await FranjasPorDefectoSeeder.SembrarAsync(db);
        await FranjasPorDefectoSeeder.SembrarAsync(db);

        Assert.Equal(4, await db.FranjasHorarias.CountAsync());
    }
}
