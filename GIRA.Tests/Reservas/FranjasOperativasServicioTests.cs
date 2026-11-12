using GIRA.Application.Services.Reservas;
using GIRA.Application.Validations.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Repositories.Reservas;
using GIRA.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Tests.Reservas;

/// <summary>Pruebas del servicio de franjas para el personal operativo (GI-399): orden, inactivas, borradas y sin paginar.</summary>
public class FranjasOperativasServicioTests
{
    private sealed class Entorno : IDisposable
    {
        public GiraDbContext Contexto { get; }
        public FranjaService Franjas { get; }

        public Entorno()
        {
            var opciones = new DbContextOptionsBuilder<GiraDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            Contexto = new GiraDbContext(opciones);

            var reloj = new TimeProviderFalso(new DateTimeOffset(2026, 12, 1, 16, 0, 0, TimeSpan.Zero));
            Franjas = new FranjaService(
                new FranjaGestionRepository(Contexto),
                new ReservasFuturasRepository(Contexto),
                new GuardarFranjaRequestValidator(),
                new ListarSalonRequestValidator(),
                reloj);
        }

        public void Dispose() => Contexto.Dispose();

        public async Task<FranjaHoraria> SembrarAsync(
            string nombre, int horaInicio, int horaFin, bool activa = true, bool borrada = false)
        {
            var franja = new FranjaHoraria
            {
                Id = Guid.NewGuid(),
                Nombre = nombre,
                HoraInicio = new TimeOnly(horaInicio, 0),
                HoraFin = new TimeOnly(horaFin, 0),
                Activa = activa,
                IsDeleted = borrada
            };
            Contexto.FranjasHorarias.Add(franja);
            await Contexto.SaveChangesAsync();
            return franja;
        }
    }

    [Fact]
    public async Task Devuelve_Las_Franjas_Ordenadas_Por_Hora_De_Inicio_Aunque_Se_Sembraron_Desordenadas()
    {
        using var e = new Entorno();
        await e.SembrarAsync("Cena", 18, 22);
        await e.SembrarAsync("Brunch", 9, 11);
        await e.SembrarAsync("Almuerzo", 12, 15);

        var resultado = await e.Franjas.ListarOperativasAsync(default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(new[] { "Brunch", "Almuerzo", "Cena" }, resultado.Value!.Select(f => f.Nombre));
    }

    [Fact]
    public async Task Incluye_Las_Inactivas_Con_Su_Bandera()
    {
        using var e = new Entorno();
        await e.SembrarAsync("Almuerzo", 12, 15);
        await e.SembrarAsync("Cena", 18, 22, activa: false);

        var resultado = await e.Franjas.ListarOperativasAsync(default);

        var franjas = resultado.Value!;
        Assert.Equal(2, franjas.Count);
        Assert.True(franjas.Single(f => f.Nombre == "Almuerzo").Activa);
        Assert.False(franjas.Single(f => f.Nombre == "Cena").Activa);
    }

    [Fact]
    public async Task Excluye_Las_Borradas()
    {
        using var e = new Entorno();
        await e.SembrarAsync("Almuerzo", 12, 15);
        await e.SembrarAsync("Borrada", 16, 17, borrada: true);

        var resultado = await e.Franjas.ListarOperativasAsync(default);

        Assert.Equal(new[] { "Almuerzo" }, resultado.Value!.Select(f => f.Nombre));
    }

    [Fact]
    public async Task No_Pagina_Y_Devuelve_Todas_Aunque_Superen_El_Tamano_De_Pagina_Por_Defecto()
    {
        using var e = new Entorno();
        // 25 > 20 (pageSize por defecto del listado de administración).
        for (var i = 0; i < 25; i++)
        {
            e.Contexto.FranjasHorarias.Add(new FranjaHoraria
            {
                Id = Guid.NewGuid(),
                Nombre = $"Franja {i:00}",
                HoraInicio = new TimeOnly(i % 24, i),
                HoraFin = new TimeOnly(i % 24, i + 1)
            });
        }
        await e.Contexto.SaveChangesAsync();

        var resultado = await e.Franjas.ListarOperativasAsync(default);

        Assert.Equal(25, resultado.Value!.Count);
    }

    [Fact]
    public async Task El_Dto_Trae_Solo_Los_Cinco_Campos_Y_Los_Mapea_Bien()
    {
        using var e = new Entorno();
        var franja = await e.SembrarAsync("Almuerzo", 12, 15);

        var resultado = await e.Franjas.ListarOperativasAsync(default);

        var dto = Assert.Single(resultado.Value!);
        Assert.Equal(franja.Id, dto.Id);
        Assert.Equal("Almuerzo", dto.Nombre);
        Assert.Equal(new TimeOnly(12, 0), dto.HoraInicio);
        Assert.Equal(new TimeOnly(15, 0), dto.HoraFin);
        Assert.True(dto.Activa);
        Assert.Equal(
            new[] { "Activa", "HoraFin", "HoraInicio", "Id", "Nombre" },
            typeof(GIRA.Application.DTOs.Reservas.FranjaOperativaDto)
                .GetProperties().Select(p => p.Name).Where(n => n != "EqualityContract").Order());
    }
}
