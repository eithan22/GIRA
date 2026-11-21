using GIRA.Application.DTOs.Analitica;
using GIRA.Application.Services.Analitica;

namespace GIRA.Tests.Analitica;

public sealed class PrediccionBaseTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 20);
    private static readonly Guid Franja = Guid.NewGuid();

    [Fact]
    public void GeneraPorDiaYFranjaUsandoSoloFechasObservadas()
    {
        var historial = new[]
        {
            Registro(Hoy.AddDays(-6), Franja, 10, 1, 0),
            Registro(Hoy.AddDays(-13), Franja, 11, 0, 11),
            Registro(Hoy.AddDays(-20), Franja, 0, 0, 0),
            Registro(Hoy.AddDays(-34), Franja, 100, 1, 0)
        };
        var generacion = Guid.NewGuid();
        var predicciones = PrediccionBase.Generar(Hoy, DateTimeOffset.UtcNow, generacion, [Franja], historial);

        Assert.Equal(2, predicciones.Count);
        Assert.All(predicciones, prediccion =>
        {
            Assert.Equal(11, prediccion.AfluenciaEstimada);
            Assert.True(prediccion.BajaConfianza);
            Assert.Equal(generacion, prediccion.GeneracionId);
            Assert.Null(prediccion.LimiteInferior);
            Assert.Null(prediccion.LimiteSuperior);
        });
        Assert.Equal(Hoy.AddDays(1), predicciones[0].Fecha);
        Assert.Equal(Hoy.AddDays(8), predicciones[1].Fecha);
    }

    [Fact]
    public void SinVisitasObservadas_NoInventaPrediccion()
    {
        var soloNoShow = Registro(Hoy.AddDays(-6), Franja, 0, 0, 0);
        Assert.Empty(PrediccionBase.Generar(Hoy, DateTimeOffset.UtcNow, Guid.NewGuid(), [Franja], [soloNoShow]));
    }

    private static HistorialAfluenciaDto Registro(DateOnly fecha, Guid franja, int total, int reservas, int sinReserva) =>
        new(fecha, franja, total - sinReserva, sinReserva, total, reservas, 1);
}
