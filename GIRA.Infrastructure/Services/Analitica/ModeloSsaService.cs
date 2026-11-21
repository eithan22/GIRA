using GIRA.Application.Interfaces.Services.Analitica;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.TimeSeries;

namespace GIRA.Infrastructure.Services.Analitica;

public sealed class ModeloSsaService : IModeloSsaService
{
    public EntrenamientoSsa Entrenar(IReadOnlyList<float> observaciones, CancellationToken cancellationToken)
    {
        ValidarObservaciones(observaciones, ModeloSsaParametros.DiasEntrenamientoMinimos);
        cancellationToken.ThrowIfCancellationRequested();

        var contexto = CrearContexto();
        var datos = contexto.Data.LoadFromEnumerable(observaciones.Select(valor => new Observacion { Valor = valor }));
        var estimador = contexto.Forecasting.ForecastBySsa(
            outputColumnName: nameof(PronosticoSsaDatos.Pronostico),
            inputColumnName: nameof(Observacion.Valor),
            windowSize: 7,
            seriesLength: ModeloSsaParametros.DiasEntrenamientoMinimos,
            trainSize: observaciones.Count,
            horizon: ModeloSsaParametros.HorizonteDias,
            confidenceLevel: 0.95f,
            confidenceLowerBoundColumn: nameof(PronosticoSsaDatos.LimiteInferior),
            confidenceUpperBoundColumn: nameof(PronosticoSsaDatos.LimiteSuperior));
        var modelo = estimador.Fit(datos);
        cancellationToken.ThrowIfCancellationRequested();

        using var archivo = new MemoryStream();
        contexto.Model.Save(modelo, datos.Schema, archivo);
        using var motor = modelo.CreateTimeSeriesEngine<Observacion, PronosticoSsaDatos>(contexto);
        var pronostico = motor.Predict();
        return new EntrenamientoSsa(archivo.ToArray(), pronostico.Pronostico,
            pronostico.LimiteInferior, pronostico.LimiteSuperior);
    }

    public PronosticoSsa Predecir(byte[] archivoModelo, IReadOnlyList<float> nuevasObservaciones,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(archivoModelo);
        ArgumentNullException.ThrowIfNull(nuevasObservaciones);
        var contexto = CrearContexto();
        using var archivo = new MemoryStream(archivoModelo, writable: false);
        var modelo = contexto.Model.Load(archivo, out _);
        using var motor = modelo.CreateTimeSeriesEngine<Observacion, PronosticoSsaDatos>(contexto);
        foreach (var valor in nuevasObservaciones)
        {
            cancellationToken.ThrowIfCancellationRequested();
            ValidarObservacion(valor);
            _ = motor.Predict(new Observacion { Valor = valor });
        }

        var pronostico = motor.Predict();
        return new PronosticoSsa(pronostico.Pronostico, pronostico.LimiteInferior,
            pronostico.LimiteSuperior);
    }

    private static MLContext CrearContexto() => new(seed: 42);

    private static void ValidarObservaciones(IReadOnlyList<float> observaciones, int minimo)
    {
        ArgumentNullException.ThrowIfNull(observaciones);
        if (observaciones.Count < minimo)
            throw new ArgumentException($"SSA requiere al menos {minimo} observaciones.", nameof(observaciones));
        foreach (var observacion in observaciones)
            ValidarObservacion(observacion);
    }

    private static void ValidarObservacion(float observacion)
    {
        if (!float.IsFinite(observacion) || observacion < 0)
            throw new ArgumentOutOfRangeException(nameof(observacion), "Las observaciones deben ser finitas y no negativas.");
    }

    private sealed class Observacion
    {
        public float Valor { get; set; }
    }

    private sealed class PronosticoSsaDatos
    {
        [VectorType(ModeloSsaParametros.HorizonteDias)]
        public float[] Pronostico { get; set; } = [];

        [VectorType(ModeloSsaParametros.HorizonteDias)]
        public float[] LimiteInferior { get; set; } = [];

        [VectorType(ModeloSsaParametros.HorizonteDias)]
        public float[] LimiteSuperior { get; set; } = [];
    }
}
