using GIRA.Application.Events.Analitica;
using GIRA.Domain.Entities.Analitica;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Repositories.Analitica;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;

namespace GIRA.Tests.Analitica;

public sealed class GeneracionPrediccionRepositoryTests
{
    private static readonly Guid RestauranteId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly IConfiguration Configuracion = new ConfigurationBuilder().AddInMemoryCollection(
        new Dictionary<string, string?> { ["Analitica:RestauranteId"] = RestauranteId.ToString() }).Build();

    [Fact]
    public async Task Guardar_ReemplazaSoloLasPrediccionesParaLasQueExisteUnNuevoValor()
    {
        var opciones = new DbContextOptionsBuilder<GiraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var contexto = new GiraDbContext(opciones);
        var fecha = new DateOnly(2026, 11, 21);
        var franjaReemplazada = Guid.NewGuid();
        var franjaSinDatosNuevos = Guid.NewGuid();
        var anterior = Prediccion(fecha, franjaReemplazada);
        var conservada = Prediccion(fecha, franjaSinDatosNuevos);
        contexto.Set<PrediccionAfluencia>().AddRange(anterior, conservada);
        await contexto.SaveChangesAsync();

        var generacion = new GeneracionPrediccion
        {
            Id = Guid.NewGuid(), FechaProceso = fecha.AddDays(-1), GeneradaEn = DateTimeOffset.UtcNow,
            CantidadPredicciones = 1
        };
        var nueva = Prediccion(fecha, franjaReemplazada);
        nueva.GeneracionId = generacion.Id;
        await new GeneracionPrediccionRepository(contexto, Configuracion).GuardarAsync(generacion, [nueva], [], default);
        contexto.ChangeTracker.Clear();

        var guardadas = await contexto.Set<PrediccionAfluencia>().ToListAsync();
        Assert.False(guardadas.Single(p => p.Id == anterior.Id).Vigente);
        Assert.True(guardadas.Single(p => p.Id == conservada.Id).Vigente);
        Assert.True(guardadas.Single(p => p.Id == nueva.Id).Vigente);
    }

    [Fact]
    public async Task Guardar_SinPredicciones_NoInvalidaLasExistentes()
    {
        var opciones = new DbContextOptionsBuilder<GiraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var contexto = new GiraDbContext(opciones);
        var fecha = new DateOnly(2026, 11, 21);
        var anterior = Prediccion(fecha, Guid.NewGuid());
        contexto.Set<PrediccionAfluencia>().Add(anterior);
        await contexto.SaveChangesAsync();

        await new GeneracionPrediccionRepository(contexto, Configuracion).GuardarAsync(new GeneracionPrediccion
        {
            Id = Guid.NewGuid(), FechaProceso = fecha.AddDays(-1), GeneradaEn = DateTimeOffset.UtcNow
        }, [], [], default);
        contexto.ChangeTracker.Clear();

        Assert.True((await contexto.Set<PrediccionAfluencia>().SingleAsync()).Vigente);
    }

    [Fact]
    public async Task Publicacion_ConservaIdentidadYMetadatosDeCadaPrediccionEnLosReintentos()
    {
        var opciones = new DbContextOptionsBuilder<GiraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var contexto = new GiraDbContext(opciones);
        var fecha = new DateOnly(2026, 11, 21);
        var generacion = new GeneracionPrediccion
        {
            Id = Guid.NewGuid(), FechaProceso = fecha.AddDays(-1),
            GeneradaEn = new DateTimeOffset(2026, 11, 21, 3, 0, 0, TimeSpan.Zero)
        };
        var primera = Prediccion(fecha, Guid.NewGuid());
        var segunda = Prediccion(fecha, Guid.NewGuid());
        primera.GeneracionId = segunda.GeneracionId = generacion.Id;
        primera.VersionModelo = "modelo-ssa-v1";
        segunda.VersionModelo = "promedio-semanal-v1";
        var publicacion = new PublicacionPrediccion { Id = Guid.NewGuid(), GeneracionId = generacion.Id, Fecha = fecha };
        contexto.Set<GeneracionPrediccion>().Add(generacion);
        contexto.Set<PrediccionAfluencia>().AddRange(primera, segunda);
        contexto.Set<PublicacionPrediccion>().Add(publicacion);
        await contexto.SaveChangesAsync();
        var repositorio = new GeneracionPrediccionRepository(contexto, Configuracion);

        var primerEnvio = Assert.Single(await repositorio.ObtenerPendientesAsync(default)).Evento;
        var reintento = Assert.Single(await repositorio.ObtenerPendientesAsync(default)).Evento;

        Assert.Equal(publicacion.Id, primerEnvio.EventoId);
        Assert.Equal(primerEnvio.EventoId, reintento.EventoId);
        Assert.Equal(1, primerEnvio.VersionContrato);
        Assert.Equal(nameof(PrediccionAfluenciaGeneradaEvent), primerEnvio.TipoEvento);
        Assert.Equal(RestauranteId, primerEnvio.RestauranteId);
        Assert.Equal(generacion.GeneradaEn, primerEnvio.OcurridoEnUtc);
        Assert.Contains(primerEnvio.Predicciones, p => p.PrediccionId == primera.Id && p.VersionModelo == primera.VersionModelo);
        Assert.Contains(primerEnvio.Predicciones, p => p.PrediccionId == segunda.Id && p.VersionModelo == segunda.VersionModelo);
    }

    [Fact]
    public async Task PublicacionPendiente_SinRestaurante_NoDevuelveUnEventoInvalido()
    {
        var opciones = new DbContextOptionsBuilder<GiraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        await using var contexto = new GiraDbContext(opciones);
        contexto.Set<PublicacionPrediccion>().Add(new PublicacionPrediccion
        {
            Id = Guid.NewGuid(), GeneracionId = Guid.NewGuid(), Fecha = new DateOnly(2026, 11, 21)
        });
        await contexto.SaveChangesAsync();
        var sinRestaurante = new ConfigurationBuilder().Build();

        var error = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            new GeneracionPrediccionRepository(contexto, sinRestaurante).ObtenerPendientesAsync(default));
        Assert.Contains("Analitica:RestauranteId", error.Message);
        Assert.Null((await contexto.Set<PublicacionPrediccion>().SingleAsync()).PublicadaEn);
    }

    private static PrediccionAfluencia Prediccion(DateOnly fecha, Guid franja) => new()
    {
        Id = Guid.NewGuid(), GeneracionId = Guid.NewGuid(), Fecha = fecha, FranjaHorariaId = franja,
        AfluenciaEstimada = 5, GeneradaEn = DateTimeOffset.UtcNow, VersionModelo = "base",
        BajaConfianza = true, Vigente = true
    };
}
