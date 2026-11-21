using GIRA.Application.DTOs.Analitica;
using GIRA.Application.Services.Analitica;

namespace GIRA.Tests.Analitica;

public sealed class ReglasRecomendacionesTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 20);
    private static readonly Guid Franja = Guid.NewGuid();

    [Fact]
    public void AplicaLasCuatroReglasYRespetaSusLimites()
    {
        var historial = new[]
        {
            Registro(Hoy.AddDays(-6), 10, 4, 1),
            Registro(Hoy.AddDays(-13), 10, 0, 0)
        };
        var prediccion = Prediccion(Hoy.AddDays(1), 13, true);
        var resultado = ReglasRecomendaciones.Calcular(Hoy, historial, [prediccion],
            [new AlertaRecurrente(Guid.NewGuid(), "Insumo", 3)], new());
        Assert.Equal(4, resultado.Count);
        Assert.Contains(resultado, r => r.Clave.StartsWith("demanda:"));
        Assert.Contains(resultado, r => r.Clave.StartsWith("confianza:"));
        Assert.Contains(resultado, r => r.Clave.StartsWith("no-show:"));
        Assert.Contains(resultado, r => r.Clave.StartsWith("alertas:"));
    }

    [Fact]
    public void SinDatosOEnUmbralesEstrictos_NoInventaRecomendaciones()
    {
        var historial = new[] { Registro(Hoy.AddDays(-6), 10, 17, 3) };
        var resultado = ReglasRecomendaciones.Calcular(Hoy, historial, [Prediccion(Hoy.AddDays(1), 12, false)],
            [new AlertaRecurrente(Guid.NewGuid(), "Insumo", 2)], new());
        Assert.Empty(resultado);
        Assert.Empty(ReglasRecomendaciones.Calcular(Hoy, [], [Prediccion(Hoy.AddDays(1), 100, false)], [], new()));
    }

    [Fact]
    public void UtilizaSoloObservacionesDelMismoDiaYUmbralesConfigurados()
    {
        var historial = new[]
        {
            Registro(Hoy.AddDays(-6), 10, 1, 0),
            Registro(Hoy.AddDays(-5), 100, 1, 0),
            Registro(Hoy.AddDays(-34), 100, 1, 0)
        };
        var opciones = new OpcionesRecomendaciones { PorcentajeAumentoDemanda = 50 };
        Assert.Empty(ReglasRecomendaciones.Calcular(Hoy, historial, [Prediccion(Hoy.AddDays(1), 14, false)], [], opciones));
        Assert.Single(ReglasRecomendaciones.Calcular(Hoy, historial, [Prediccion(Hoy.AddDays(1), 15, false)], [], opciones));
    }

    private static HistorialAfluenciaDto Registro(DateOnly fecha, int total, int completadas, int ausencias) =>
        new(fecha, Franja, total, 0, total, completadas, ausencias);

    private static PrediccionAfluenciaDto Prediccion(DateOnly fecha, int cantidad, bool baja) =>
        new(fecha, Franja, "Almuerzo", cantidad, null, null, baja, DateTimeOffset.UtcNow, "modelo");
}
