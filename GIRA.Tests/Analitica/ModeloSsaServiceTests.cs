using GIRA.Application.Interfaces.Services.Analitica;
using GIRA.Infrastructure.Services.Analitica;

namespace GIRA.Tests.Analitica;

public sealed class ModeloSsaServiceTests
{
    [Fact]
    public void EntrenarYPredecir_DevuelveHorizonteYLimitesDel95PorCiento()
    {
        var servicio = new ModeloSsaService();
        var entrenamiento = Enumerable.Range(0, ModeloSsaParametros.DiasEntrenamientoMinimos)
            .Select(dia => (float)(20 + dia % 7 * 3 + dia / 7))
            .ToList();
        var resultado = servicio.Entrenar(entrenamiento, CancellationToken.None);

        Assert.NotEmpty(resultado.ArchivoModelo);
        Assert.Equal(ModeloSsaParametros.HorizonteDias, resultado.Pronostico.Count);
        Assert.Equal(ModeloSsaParametros.HorizonteDias, resultado.LimiteInferior.Count);
        Assert.Equal(ModeloSsaParametros.HorizonteDias, resultado.LimiteSuperior.Count);

        var pronostico = servicio.Predecir(resultado.ArchivoModelo,
            Enumerable.Range(56, 14).Select(dia => (float)(20 + dia % 7 * 3 + dia / 7)).ToList(),
            CancellationToken.None);
        Assert.Equal(ModeloSsaParametros.HorizonteDias, pronostico.Pronostico.Count);
        Assert.Equal(ModeloSsaParametros.HorizonteDias, pronostico.LimiteInferior.Count);
        Assert.Equal(ModeloSsaParametros.HorizonteDias, pronostico.LimiteSuperior.Count);
    }

    [Fact]
    public void Entrenar_ExigeLos56DiasDeEntrenamiento()
    {
        var servicio = new ModeloSsaService();

        Assert.Throws<ArgumentException>(() =>
            servicio.Entrenar(Enumerable.Repeat(1f, 55).ToList(), CancellationToken.None));
    }
}
