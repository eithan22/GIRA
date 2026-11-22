using GIRA.Domain.Entities.Analitica;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Repositories.Analitica;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GIRA.Tests.Analitica;

public sealed class GeneracionPrediccionRepositoryTests
{
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
        await new GeneracionPrediccionRepository(contexto).GuardarAsync(generacion, [nueva], [], default);
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

        await new GeneracionPrediccionRepository(contexto).GuardarAsync(new GeneracionPrediccion
        {
            Id = Guid.NewGuid(), FechaProceso = fecha.AddDays(-1), GeneradaEn = DateTimeOffset.UtcNow
        }, [], [], default);
        contexto.ChangeTracker.Clear();

        Assert.True((await contexto.Set<PrediccionAfluencia>().SingleAsync()).Vigente);
    }

    private static PrediccionAfluencia Prediccion(DateOnly fecha, Guid franja) => new()
    {
        Id = Guid.NewGuid(), GeneracionId = Guid.NewGuid(), Fecha = fecha, FranjaHorariaId = franja,
        AfluenciaEstimada = 5, GeneradaEn = DateTimeOffset.UtcNow, VersionModelo = "base",
        BajaConfianza = true, Vigente = true
    };
}
