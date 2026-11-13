using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Services.Reservas;
using GIRA.Application.Validations.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Repositories.Reservas;
using GIRA.Infrastructure.Services.Reservas;
using GIRA.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Pruebas del listado de visitas sin reserva (GI-402) en <see cref="VisitaService"/>: fecha por defecto en hora de
/// Santo Domingo, rango de la fecha, hora de registro en hora local, tope de 200 con bandera de recorte, y que la
/// respuesta no lleva datos personales. Repositorios reales sobre EF Core InMemory y reloj simulado (UTC-4).
/// </summary>
public class VisitaListadoServicioTests
{
    // 13:00 locales del 1 de diciembre de 2026.
    private static readonly DateOnly Dia = new(2026, 12, 1);
    private static readonly DateTimeOffset Ahora = new(2026, 12, 1, 17, 0, 0, TimeSpan.Zero);

    private sealed class Entorno : IDisposable
    {
        public GiraDbContext Contexto { get; }
        public VisitaService Servicio { get; }
        public FranjaHoraria Almuerzo { get; } = new()
        {
            Id = Guid.NewGuid(), Nombre = "Almuerzo", HoraInicio = new TimeOnly(12, 0), HoraFin = new TimeOnly(15, 0)
        };
        public Mesa Mesa { get; }

        public Entorno(DateTimeOffset ahoraUtc)
        {
            var opciones = new DbContextOptionsBuilder<GiraDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
            Contexto = new GiraDbContext(opciones);
            var reloj = new TimeProviderFalso(ahoraUtc);
            Servicio = new VisitaService(
                new VisitaRepository(Contexto),
                new FranjaGestionRepository(Contexto),
                new MesaGestionRepository(Contexto),
                new ZonaRepository(Contexto),
                new ReservaRepository(Contexto),
                new RegistrarVisitaRequestValidator(),
                new ListarVisitasRequestValidator(reloj),
                new PublicadorEventosSeguro(new PublisherFalso(), NullLogger<PublicadorEventosSeguro>.Instance),
                reloj);

            var zona = new Zona { Id = Guid.NewGuid(), Nombre = "Salón" };
            Mesa = new Mesa { Id = Guid.NewGuid(), Numero = 12, Capacidad = 6, ZonaId = zona.Id };
            Contexto.AddRange(zona, Mesa, Almuerzo);
        }

        public async Task SembrarAsync(int personas, DateTime? creadaUtc, DateOnly? fecha = null, bool conMesa = true)
        {
            Contexto.VisitasSinReserva.Add(new VisitaSinReserva
            {
                Id = Guid.NewGuid(), Fecha = fecha ?? Dia, FranjaHorariaId = Almuerzo.Id, MesaId = conMesa ? Mesa.Id : null,
                PersonasReales = personas, CreatedAt = creadaUtc ?? default
            });
            await Contexto.SaveChangesAsync();
        }

        public Task<OperationResult<VisitasDelDiaDto>> ListarAsync(DateOnly? fecha = null) =>
            Servicio.ListarAsync(new ListarVisitasRequest { Fecha = fecha?.ToString("yyyy-MM-dd") }, default);

        public Task<OperationResult<VisitasDelDiaDto>> ListarTextoAsync(string? fecha) =>
            Servicio.ListarAsync(new ListarVisitasRequest { Fecha = fecha }, default);

        public void Dispose() => Contexto.Dispose();
    }

    private static DateTime Utc(int dia, int hora, int minuto) => new(2026, 12, dia, hora, minuto, 0, DateTimeKind.Utc);

    // --- Fecha por defecto ---

    [Fact]
    public async Task Sin_Fecha_Lista_Las_De_Hoy_Y_No_Las_De_Otros_Dias()
    {
        using var e = new Entorno(Ahora);
        await e.SembrarAsync(2, Utc(1, 16, 0));
        await e.SembrarAsync(3, Utc(1, 16, 30), Dia.AddDays(-1));

        var resultado = await e.ListarAsync();

        Assert.True(resultado.IsSuccess);
        Assert.Equal(Dia, resultado.Value!.Fecha);
        Assert.Equal(2, Assert.Single(resultado.Value.Visitas).Personas);
    }

    [Fact]
    public async Task Sin_Fecha_Hoy_Es_El_De_Santo_Domingo_Aunque_Ya_Sea_El_Dia_Siguiente_En_Utc()
    {
        // 01:30 UTC del 2 de diciembre = 21:30 del 1 de diciembre en Santo Domingo: "hoy" sigue siendo el 1.
        using var e = new Entorno(new DateTimeOffset(2026, 12, 2, 1, 30, 0, TimeSpan.Zero));
        await e.SembrarAsync(4, Utc(2, 1, 0), Dia);
        await e.SembrarAsync(9, Utc(2, 1, 10), Dia.AddDays(1));

        var resultado = await e.ListarAsync();

        Assert.Equal(Dia, resultado.Value!.Fecha);
        Assert.Equal(4, resultado.Value.TotalPersonas);
    }

    [Fact]
    public async Task Con_Fecha_Explicita_Lista_Ese_Dia()
    {
        using var e = new Entorno(Ahora);
        await e.SembrarAsync(2, Utc(1, 16, 0));
        await e.SembrarAsync(7, Utc(1, 15, 0), Dia.AddDays(-3));

        var resultado = await e.ListarAsync(Dia.AddDays(-3));

        Assert.Equal(Dia.AddDays(-3), resultado.Value!.Fecha);
        Assert.Equal(7, Assert.Single(resultado.Value.Visitas).Personas);
    }

    // --- Totales, orden y hora local ---

    [Fact]
    public async Task Devuelve_Total_De_Visitas_Total_De_Personas_Y_Las_Mas_Recientes_Primero()
    {
        using var e = new Entorno(Ahora);
        await e.SembrarAsync(2, Utc(1, 16, 10)); // 12:10
        await e.SembrarAsync(4, Utc(1, 17, 5)); // 13:05
        await e.SembrarAsync(6, Utc(1, 16, 45), conMesa: false); // 12:45

        var dia = (await e.ListarAsync()).Value!;

        Assert.Equal(3, dia.TotalVisitas);
        Assert.Equal(12, dia.TotalPersonas);
        Assert.False(dia.Truncada);
        Assert.Equal([4, 6, 2], dia.Visitas.Select(v => v.Personas));
        Assert.Equal([new TimeOnly(13, 5), new TimeOnly(12, 45), new TimeOnly(12, 10)], dia.Visitas.Select(v => v.HoraRegistro!.Value));
        Assert.Equal([12, null, 12], dia.Visitas.Select(v => v.MesaNumero));
        Assert.All(dia.Visitas, v => Assert.Equal("Almuerzo", v.FranjaNombre));
    }

    [Fact]
    public async Task La_Hora_De_Registro_Es_Local_Y_Una_Visita_De_La_Noche_No_Cambia_De_Dia()
    {
        // Registrada a las 21:30 locales del 1 de diciembre = 01:30 UTC del 2 de diciembre. Ahora son las 02:00
        // locales del 2 de diciembre (06:00 UTC), así que el 1 es "ayer" y el 2 es "hoy".
        using var e = new Entorno(new DateTimeOffset(2026, 12, 2, 6, 0, 0, TimeSpan.Zero));
        await e.SembrarAsync(3, Utc(2, 1, 30), Dia);

        var delPrimero = (await e.ListarAsync(Dia)).Value!;
        var delSegundo = (await e.ListarAsync(Dia.AddDays(1))).Value!;

        Assert.Equal(new TimeOnly(21, 30), Assert.Single(delPrimero.Visitas).HoraRegistro);
        Assert.Equal(0, delSegundo.TotalVisitas);
    }

    [Fact]
    public async Task Sin_Auditoria_La_Hora_De_Registro_Es_Null_En_Vez_De_Una_Hora_Falsa()
    {
        using var e = new Entorno(Ahora);
        await e.SembrarAsync(2, creadaUtc: null);

        var visita = Assert.Single((await e.ListarAsync()).Value!.Visitas);

        Assert.Null(visita.HoraRegistro);
    }

    [Fact]
    public async Task Un_Dia_Sin_Visitas_Devuelve_Ceros_Y_Lista_Vacia()
    {
        using var e = new Entorno(Ahora);

        var dia = (await e.ListarAsync()).Value!;

        Assert.Equal(0, dia.TotalVisitas);
        Assert.Equal(0, dia.TotalPersonas);
        Assert.False(dia.Truncada);
        Assert.Empty(dia.Visitas);
    }

    // --- Tope de 200 ---

    [Fact]
    public async Task Con_Mas_De_200_Visitas_Trae_200_Los_Totales_Cuentan_Todas_Y_Marca_Truncada()
    {
        using var e = new Entorno(Ahora);
        for (var i = 0; i < 205; i++)
        {
            e.Contexto.VisitasSinReserva.Add(new VisitaSinReserva
            {
                Id = Guid.NewGuid(), Fecha = Dia, FranjaHorariaId = e.Almuerzo.Id, PersonasReales = 2,
                CreatedAt = Utc(1, 14, 0).AddSeconds(i)
            });
        }
        await e.Contexto.SaveChangesAsync();

        var dia = (await e.ListarAsync()).Value!;

        Assert.Equal(VisitaService.TopeListado, dia.Visitas.Count);
        Assert.Equal(205, dia.TotalVisitas);
        Assert.Equal(410, dia.TotalPersonas);
        Assert.True(dia.Truncada);
        // Quedan las más recientes: la de i = 204 es la primera.
        Assert.Equal(new TimeOnly(10, 3, 24), dia.Visitas[0].HoraRegistro);
    }

    [Fact]
    public async Task Con_Exactamente_200_Visitas_No_Se_Marca_Truncada()
    {
        using var e = new Entorno(Ahora);
        for (var i = 0; i < 200; i++)
        {
            e.Contexto.VisitasSinReserva.Add(new VisitaSinReserva
            {
                Id = Guid.NewGuid(), Fecha = Dia, FranjaHorariaId = e.Almuerzo.Id, PersonasReales = 1,
                CreatedAt = Utc(1, 14, 0).AddSeconds(i)
            });
        }
        await e.Contexto.SaveChangesAsync();

        var dia = (await e.ListarAsync()).Value!;

        Assert.Equal(200, dia.Visitas.Count);
        Assert.False(dia.Truncada);
    }

    // --- Rango de la fecha ---

    [Fact]
    public async Task Una_Fecha_Futura_Es_Validation_Y_hoy_Es_Valido()
    {
        using var e = new Entorno(Ahora);

        var manana = await e.ListarAsync(Dia.AddDays(1));
        var hoy = await e.ListarAsync(Dia);

        Assert.Equal(ErrorType.Validation, manana.Type);
        Assert.Equal("La fecha no puede ser futura.", manana.Error);
        Assert.True(hoy.IsSuccess);
    }

    [Fact]
    public async Task Hasta_365_Dias_Atras_Es_Valido_Y_366_Es_Validation()
    {
        using var e = new Entorno(Ahora);

        var limite = await e.ListarAsync(Dia.AddDays(-365));
        var pasado = await e.ListarAsync(Dia.AddDays(-366));

        Assert.True(limite.IsSuccess);
        Assert.Equal(ErrorType.Validation, pasado.Type);
        Assert.Equal("La fecha no puede ser de hace más de 365 días.", pasado.Error);
    }

    [Theory]
    [InlineData("abc")]
    [InlineData("2026-13-45")]
    [InlineData("01/12/2026")]      // ambiguo: el binding de DateOnly lo leería como 12 de enero
    [InlineData("12/01/2026")]
    [InlineData("2026-1-5")]        // sin ceros
    [InlineData("20261201")]
    [InlineData("2026-12-01T00:00:00")]
    public async Task Un_Formato_Que_No_Es_Exactamente_Aaaa_Mm_Dd_Es_Validation(string fecha)
    {
        using var e = new Entorno(Ahora);

        var resultado = await e.ListarTextoAsync(fecha);

        Assert.Equal(ErrorType.Validation, resultado.Type);
        Assert.Equal("La fecha debe tener el formato aaaa-mm-dd.", resultado.Error);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Una_Fecha_Vacia_Se_Trata_Como_Sin_Fecha_Y_Usa_Hoy(string? fecha)
    {
        using var e = new Entorno(Ahora);

        var resultado = await e.ListarTextoAsync(fecha);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(Dia, resultado.Value!.Fecha);
    }

    [Fact]
    public async Task Una_Fecha_Con_Espacios_Alrededor_Se_Acepta()
    {
        using var e = new Entorno(Ahora);

        var resultado = await e.ListarTextoAsync(" 2026-12-01 ");

        Assert.Equal(Dia, resultado.Value!.Fecha);
    }

    // --- Sin datos personales ---

    [Fact]
    public async Task La_Respuesta_No_Tiene_Datos_Personales_Ni_Del_Usuario_Que_Registra()
    {
        Assert.Equal(
            ["Fecha", "TotalVisitas", "TotalPersonas", "Truncada", "Visitas"],
            typeof(VisitasDelDiaDto).GetProperties().Select(p => p.Name));
        Assert.Equal(
            ["Id", "FranjaNombre", "MesaNumero", "Personas", "HoraRegistro"],
            typeof(VisitaListadoDto).GetProperties().Select(p => p.Name));
        await Task.CompletedTask;
    }

    // --- Coherencia con el registro (el POST no cambia) ---

    [Fact]
    public async Task Una_Visita_Registrada_Con_RegistrarAsync_Aparece_En_El_Listado_De_Hoy()
    {
        using var e = new Entorno(Ahora);
        await e.Contexto.SaveChangesAsync();

        var registrada = await e.Servicio.RegistrarAsync(new RegistrarVisitaRequest(e.Almuerzo.Id, e.Mesa.Id, 3), default);
        var dia = (await e.ListarAsync()).Value!;

        Assert.True(registrada.IsSuccess);
        Assert.Equal(1, dia.TotalVisitas);
        Assert.Equal(3, dia.TotalPersonas);
        var visita = Assert.Single(dia.Visitas);
        Assert.Equal(registrada.Value!.Id, visita.Id);
        Assert.Equal(12, visita.MesaNumero);
    }
}
