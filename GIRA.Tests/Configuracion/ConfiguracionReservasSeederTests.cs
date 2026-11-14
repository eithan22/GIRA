using GIRA.Application.Services.Reservas;
using GIRA.Domain.Entities.Configuracion;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Seed;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Tests.Configuracion;

/// <summary>
/// Seeder de la configuración de reservas. Usa la base en memoria (no toca ninguna base real);
/// los CHECK y los índices únicos de SQL Server no se prueban aquí.
/// </summary>
public class ConfiguracionReservasSeederTests
{
    private static GiraDbContext CrearContexto(string nombre)
    {
        var opciones = new DbContextOptionsBuilder<GiraDbContext>().UseInMemoryDatabase(nombre).Options;
        return new GiraDbContext(opciones);
    }

    [Fact]
    public async Task SembrarAsync_BaseVacia_CreaUnaFilaYSieteHorariosConLosDefaults()
    {
        await using var db = CrearContexto(Guid.NewGuid().ToString());

        await ConfiguracionReservasSeeder.SembrarAsync(db);

        var configuracion = await db.ConfiguracionesRestaurante.SingleAsync();
        Assert.Equal(ConfiguracionRestaurante.IdUnico, configuracion.Id);
        Assert.Equal(120, configuracion.AntelacionMinimaMinutos);
        Assert.Equal(30, configuracion.AntelacionMaximaDias);
        Assert.Equal(120, configuracion.LimiteCancelacionMinutos);
        Assert.Equal(15, configuracion.VentanaCheckInMinutos);
        Assert.Equal(2, configuracion.MaxReservasVigentesPorTelefono);
        Assert.Equal(8, configuracion.MaxPersonasPorReserva);

        var horarios = await db.HorariosOperacion.ToListAsync();
        Assert.Equal(7, horarios.Count);
        Assert.Equal(Enum.GetValues<DayOfWeek>().OrderBy(d => d), horarios.Select(h => h.DiaSemana).OrderBy(d => d));
        Assert.All(horarios, h =>
        {
            Assert.True(h.Abierto);
            Assert.Equal(new TimeOnly(12, 0), h.HoraApertura);
            Assert.Equal(new TimeOnly(22, 30), h.HoraCierre);
        });
        Assert.Empty(await db.DiasCerrados.ToListAsync());
    }

    [Fact]
    public async Task SembrarAsync_SeEjecutaDosVeces_NoDuplicaNada()
    {
        await using var db = CrearContexto(Guid.NewGuid().ToString());

        await ConfiguracionReservasSeeder.SembrarAsync(db);
        await ConfiguracionReservasSeeder.SembrarAsync(db);

        Assert.Equal(1, await db.ConfiguracionesRestaurante.CountAsync());
        Assert.Equal(7, await db.HorariosOperacion.CountAsync());
    }

    [Fact]
    public async Task SembrarAsync_ConDatosExistentes_NoModificaNada()
    {
        await using var db = CrearContexto(Guid.NewGuid().ToString());
        db.ConfiguracionesRestaurante.Add(new ConfiguracionRestaurante
        {
            Id = ConfiguracionRestaurante.IdUnico,
            AntelacionMinimaMinutos = 60,
            AntelacionMaximaDias = 90,
            LimiteCancelacionMinutos = 30,
            VentanaCheckInMinutos = 45,
            MaxReservasVigentesPorTelefono = 5,
            MaxPersonasPorReserva = 20
        });
        db.HorariosOperacion.Add(new HorarioOperacion
        {
            Id = Guid.NewGuid(), DiaSemana = DayOfWeek.Monday, Abierto = false,
            HoraApertura = new TimeOnly(9, 0), HoraCierre = new TimeOnly(17, 0)
        });
        await db.SaveChangesAsync();

        await ConfiguracionReservasSeeder.SembrarAsync(db);

        var configuracion = await db.ConfiguracionesRestaurante.SingleAsync();
        Assert.Equal(60, configuracion.AntelacionMinimaMinutos);
        Assert.Equal(90, configuracion.AntelacionMaximaDias);
        Assert.Equal(30, configuracion.LimiteCancelacionMinutos);
        Assert.Equal(45, configuracion.VentanaCheckInMinutos);
        Assert.Equal(5, configuracion.MaxReservasVigentesPorTelefono);
        Assert.Equal(20, configuracion.MaxPersonasPorReserva);

        var lunes = await db.HorariosOperacion.SingleAsync(h => h.DiaSemana == DayOfWeek.Monday);
        Assert.False(lunes.Abierto);
        Assert.Equal(new TimeOnly(9, 0), lunes.HoraApertura);
        Assert.Equal(new TimeOnly(17, 0), lunes.HoraCierre);

        // Solo se completan los 6 días que faltaban.
        Assert.Equal(7, await db.HorariosOperacion.CountAsync());
    }

    [Fact]
    public async Task SembrarAsync_ConRegistrosBorradosLogicamente_NoLosRecreaNiLosRestaura()
    {
        await using var db = CrearContexto(Guid.NewGuid().ToString());
        db.ConfiguracionesRestaurante.Add(new ConfiguracionRestaurante
        {
            Id = ConfiguracionRestaurante.IdUnico, AntelacionMaximaDias = 10, IsDeleted = true
        });
        db.HorariosOperacion.Add(new HorarioOperacion
        {
            Id = Guid.NewGuid(), DiaSemana = DayOfWeek.Sunday, Abierto = false, IsDeleted = true
        });
        await db.SaveChangesAsync();

        await ConfiguracionReservasSeeder.SembrarAsync(db);

        var configuraciones = await db.ConfiguracionesRestaurante.IgnoreQueryFilters().ToListAsync();
        var unica = Assert.Single(configuraciones);
        Assert.True(unica.IsDeleted);
        Assert.Equal(10, unica.AntelacionMaximaDias);

        var domingos = await db.HorariosOperacion.IgnoreQueryFilters()
            .Where(h => h.DiaSemana == DayOfWeek.Sunday).ToListAsync();
        var domingo = Assert.Single(domingos);
        Assert.True(domingo.IsDeleted);
        Assert.False(domingo.Abierto);
    }

    [Fact]
    public void ValoresPorDefecto_CoincidenConLosAcordados()
    {
        Assert.Equal(15, ParametrosReservaService.ValoresPorDefecto.VentanaCheckInMinutos);
        Assert.Equal(new TimeOnly(12, 0), ParametrosReservaService.ValoresPorDefecto.HoraApertura);
        Assert.Equal(new TimeOnly(22, 30), ParametrosReservaService.ValoresPorDefecto.HoraCierre);
    }
}
