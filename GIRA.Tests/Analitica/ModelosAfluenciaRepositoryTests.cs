using GIRA.Application.DTOs.Analitica;
using GIRA.Domain.Entities.Analitica;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Repositories.Analitica;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace GIRA.Tests.Analitica;

public sealed class ModelosAfluenciaRepositoryTests
{
    [Fact]
    public async Task ActivarModeloAnterior_AuditaElRollbackYConservaUnaSolaVersionActiva()
    {
        var opciones = new DbContextOptionsBuilder<GiraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var contexto = new GiraDbContext(opciones);
        var franja = Guid.NewGuid();
        var anterior = CrearModelo(franja, new DateOnly(2026, 1, 1), activo: false);
        var vigente = CrearModelo(franja, new DateOnly(2026, 3, 1), activo: true);
        contexto.Set<ModeloAfluencia>().AddRange(anterior, vigente);
        await contexto.SaveChangesAsync();

        var usuario = Guid.NewGuid();
        var fecha = new DateTimeOffset(2026, 4, 1, 12, 0, 0, TimeSpan.Zero);
        var resultado = await new ModelosAfluenciaRepository(contexto).ActivarModeloAsync(
            anterior.Id, usuario, "El modelo vigente tuvo una regresión.", fecha, esRollback: true,
            CancellationToken.None);
        contexto.ChangeTracker.Clear();

        Assert.Equal(ResultadoActivacionModelo.Activado, resultado);
        Assert.False((await contexto.Set<ModeloAfluencia>().SingleAsync(m => m.Id == vigente.Id)).Activo);
        Assert.True((await contexto.Set<ModeloAfluencia>().SingleAsync(m => m.Id == anterior.Id)).Activo);
        var auditoria = await contexto.Set<AuditoriaActivacionModelo>().SingleAsync();
        Assert.Equal(usuario, auditoria.UsuarioId);
        Assert.Equal(anterior.Id, auditoria.ModeloActivadoId);
        Assert.Equal(vigente.Id, auditoria.ModeloAnteriorId);
        Assert.True(auditoria.EsRollback);
        Assert.Equal("El modelo vigente tuvo una regresión.", auditoria.Motivo);
    }

    [Fact]
    public async Task ActivarModeloMasRecienteComoRollback_SeRechaza()
    {
        var opciones = new DbContextOptionsBuilder<GiraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var contexto = new GiraDbContext(opciones);
        var franja = Guid.NewGuid();
        var vigente = CrearModelo(franja, new DateOnly(2026, 3, 1), activo: true);
        var candidatoNuevo = CrearModelo(franja, new DateOnly(2026, 4, 1), activo: false);
        contexto.Set<ModeloAfluencia>().AddRange(vigente, candidatoNuevo);
        await contexto.SaveChangesAsync();

        var resultado = await new ModelosAfluenciaRepository(contexto).ActivarModeloAsync(
            candidatoNuevo.Id, Guid.NewGuid(), "Prueba", DateTimeOffset.UtcNow, esRollback: true,
            CancellationToken.None);

        Assert.Equal(ResultadoActivacionModelo.NoEsRollback, resultado);
        Assert.True((await contexto.Set<ModeloAfluencia>().SingleAsync(m => m.Id == vigente.Id)).Activo);
        Assert.False((await contexto.Set<ModeloAfluencia>().SingleAsync(m => m.Id == candidatoNuevo.Id)).Activo);
        Assert.Empty(await contexto.Set<AuditoriaActivacionModelo>().ToListAsync());
    }

    [Fact]
    public async Task GuardarResultados_ReemplazaLaVersionSoloCuandoMejoraLaReferencia()
    {
        var opciones = new DbContextOptionsBuilder<GiraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .ConfigureWarnings(w => w.Ignore(InMemoryEventId.TransactionIgnoredWarning))
            .Options;
        await using var contexto = new GiraDbContext(opciones);
        var franja = Guid.NewGuid();
        var usuario = Guid.NewGuid();
        var trabajoAnterior = new EntrenamientoModelo
        {
            Id = Guid.NewGuid(), SolicitadoPor = usuario, Estado = EstadoEntrenamientoModelo.Completado,
            SolicitadoEn = DateTimeOffset.UtcNow
        };
        var trabajo = new EntrenamientoModelo
        {
            Id = Guid.NewGuid(), SolicitadoPor = usuario, Estado = EstadoEntrenamientoModelo.EnCurso,
            SolicitadoEn = DateTimeOffset.UtcNow, IniciadoEn = DateTimeOffset.UtcNow
        };
        var anterior = CrearModelo(franja, new DateOnly(2026, 1, 1), activo: true);
        anterior.EntrenamientoId = trabajoAnterior.Id;
        var nueva = CrearModelo(franja, new DateOnly(2026, 3, 1), activo: true);
        nueva.EntrenamientoId = trabajo.Id;
        var resultado = new ResultadoEntrenamientoModelo
        {
            Id = Guid.NewGuid(), EntrenamientoId = trabajo.Id, FranjaHorariaId = franja,
            ModeloId = nueva.Id, Estado = EstadoResultadoEntrenamiento.Activado,
            ObservacionesEntrenamiento = 56, ObservacionesValidacion = 14,
            Mae = 1, Rmse = 1, MaeReferencia = 2, VersionModelo = nueva.Version
        };
        contexto.Set<EntrenamientoModelo>().AddRange(trabajoAnterior, trabajo);
        contexto.Set<ModeloAfluencia>().Add(anterior);
        await contexto.SaveChangesAsync();

        await new ModelosAfluenciaRepository(contexto).GuardarResultadosAsync(
            trabajo.Id, [resultado], [nueva], DateTimeOffset.UtcNow, CancellationToken.None);
        contexto.ChangeTracker.Clear();

        Assert.Equal(EstadoEntrenamientoModelo.Completado,
            (await contexto.Set<EntrenamientoModelo>().SingleAsync(e => e.Id == trabajo.Id)).Estado);
        Assert.False((await contexto.Set<ModeloAfluencia>().SingleAsync(m => m.Id == anterior.Id)).Activo);
        Assert.True((await contexto.Set<ModeloAfluencia>().SingleAsync(m => m.Id == nueva.Id)).Activo);
        Assert.Equal(56, (await contexto.Set<ResultadoEntrenamientoModelo>().SingleAsync()).ObservacionesEntrenamiento);
        var auditoria = await contexto.Set<AuditoriaActivacionModelo>().SingleAsync();
        Assert.Equal(anterior.Id, auditoria.ModeloAnteriorId);
        Assert.Equal(nueva.Id, auditoria.ModeloActivadoId);
        Assert.Equal(usuario, auditoria.UsuarioId);
        Assert.False(auditoria.EsRollback);
    }

    private static ModeloAfluencia CrearModelo(Guid franja, DateOnly creado, bool activo) => new()
    {
        Id = Guid.NewGuid(),
        EntrenamientoId = Guid.NewGuid(),
        FranjaHorariaId = franja,
        Version = Guid.NewGuid().ToString("N"),
        EntrenamientoDesde = creado.AddDays(-70),
        EntrenamientoHasta = creado.AddDays(-15),
        ValidacionDesde = creado.AddDays(-14),
        ValidacionHasta = creado.AddDays(-1),
        Mae = 1,
        Rmse = 1,
        ArchivoModelo = [1, 2, 3],
        CreadoEn = new DateTimeOffset(creado.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero),
        Activo = activo
    };
}
