using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Domain.Entities.Analitica;
using GIRA.Domain.Entities.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Repositories.Analitica;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Tests.Analitica;

public sealed class KpisPrecisionTests
{
    [Fact]
    public async Task CalculaMae_ConElUltimoPronosticoAnteriorAlInicioDelDiaLocal()
    {
        var fecha = new DateOnly(2026, 11, 18);
        var franjaId = Guid.NewGuid();
        var options = new DbContextOptionsBuilder<GiraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var contexto = new GiraDbContext(options);
        contexto.FranjasHorarias.Add(new FranjaHoraria { Id = franjaId, Nombre = "Almuerzo" });
        contexto.Set<RegistroAfluencia>().AddRange(
            new RegistroAfluencia
            {
                Id = Guid.NewGuid(), ReferenciaId = Guid.NewGuid(), Origen = OrigenRegistroAfluencia.Reserva,
                Tipo = TipoRegistroAfluencia.VisitaCompletada, Fecha = fecha, FranjaHorariaId = franjaId, Personas = 10
            },
            new RegistroAfluencia
            {
                Id = Guid.NewGuid(), ReferenciaId = Guid.NewGuid(), Origen = OrigenRegistroAfluencia.Reserva,
                Tipo = TipoRegistroAfluencia.ReservaNoAsistida, Fecha = fecha, FranjaHorariaId = franjaId, Personas = 50
            });
        contexto.Set<PrediccionAfluencia>().AddRange(
            Prediccion(fecha, franjaId, 20, new DateTimeOffset(2026, 11, 17, 12, 0, 0, TimeSpan.Zero)),
            Prediccion(fecha, franjaId, 12, new DateTimeOffset(2026, 11, 18, 3, 59, 0, TimeSpan.Zero)),
            Prediccion(fecha, franjaId, 1, new DateTimeOffset(2026, 11, 18, 4, 1, 0, TimeSpan.Zero)));
        await contexto.SaveChangesAsync();

        var resultado = await new KpisRepository(contexto)
            .CalcularPrecisionModeloAsync(fecha, fecha, default);

        Assert.Equal(2m, resultado);
    }

    [Fact]
    public async Task Precision_NoSePresentaComoCero_SiNoHayVisitasCompletadas()
    {
        var options = new DbContextOptionsBuilder<GiraDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        await using var contexto = new GiraDbContext(options);

        var resultado = await new KpisRepository(contexto).CalcularPrecisionModeloAsync(
            new DateOnly(2026, 11, 18), new DateOnly(2026, 11, 18), default);

        Assert.Null(resultado);
    }

    private static PrediccionAfluencia Prediccion(
        DateOnly fecha, Guid franjaId, int estimacion, DateTimeOffset generadaEn) => new()
    {
        Id = Guid.NewGuid(),
        GeneracionId = Guid.NewGuid(),
        Fecha = fecha,
        FranjaHorariaId = franjaId,
        AfluenciaEstimada = estimacion,
        GeneradaEn = generadaEn,
        VersionModelo = "test",
        Vigente = false
    };
}
