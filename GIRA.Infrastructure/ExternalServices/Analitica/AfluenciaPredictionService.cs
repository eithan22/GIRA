using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Application.Interfaces.Services.Analitica;
using GIRA.Domain.Entities.Analitica;
using Microsoft.ML;
using Microsoft.ML.Data;
using Microsoft.ML.Transforms.TimeSeries;
using Microsoft.Extensions.Logging;

namespace GIRA.Infrastructure.ExternalServices.Analitica;

/// <summary>Entrena SSA sobre totales diarios y distribuye el pronóstico por hora.</summary>
public sealed class AfluenciaPredictionService(
    IAnaliticaRepository repository,
    ILogger<AfluenciaPredictionService> logger) : IAfluenciaPredictionService
{
    private const int DiasPronosticados = 7;
    private const int MinimoDiasEntrenamiento = 14;
    private const string NombreModelo = "ML.NET SSA";

    /// <inheritdoc />
    public async Task GenerarPrediccionesAsync(CancellationToken cancellationToken)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var historial = await repository.ObtenerHistorialAsync(hoy.AddDays(-364), hoy, cancellationToken);
        var observaciones = CrearSerieDiaria(historial, hoy);

        if (observaciones.Count < MinimoDiasEntrenamiento)
        {
            await repository.ReemplazarPrediccionesAsync(
                hoy.AddDays(1),
                hoy.AddDays(DiasPronosticados),
                [],
                cancellationToken);
            logger.LogInformation(
                "No se generaron predicciones SSA: se requieren {Minimo} días de historial y hay {Actual}.",
                MinimoDiasEntrenamiento,
                observaciones.Count);
            return;
        }

        var ml = new MLContext(seed: 1);
        var datos = ml.Data.LoadFromEnumerable(observaciones);
        var pipeline = ml.Forecasting.ForecastBySsa(
            outputColumnName: nameof(ForecastOutput.Forecast),
            inputColumnName: nameof(DailyObservation.Total),
            windowSize: 7,
            seriesLength: Math.Min(30, observaciones.Count),
            trainSize: observaciones.Count,
            horizon: DiasPronosticados,
            isAdaptive: true);
        var modelo = pipeline.Fit(datos);
        var motor = modelo.CreateTimeSeriesEngine<DailyObservation, ForecastOutput>(ml);
        var resultado = motor.Predict().Forecast;

        if (resultado.Length < DiasPronosticados || resultado.Any(valor => !float.IsFinite(valor)))
        {
            throw new InvalidOperationException("El modelo SSA devolvió una predicción inválida.");
        }

        var distribucionPorHora = CrearDistribucionPorHora(historial);
        var generadaEn = DateTime.UtcNow;
        var predicciones = new List<PrediccionResultado>(DiasPronosticados * 24);

        for (var dia = 0; dia < DiasPronosticados; dia++)
        {
            var fecha = hoy.AddDays(dia + 1);
            var totalEstimado = (int)Math.Round(Math.Max(0, resultado[dia]), MidpointRounding.AwayFromZero);
            var cantidadesPorHora = DistribuirTotal(totalEstimado, distribucionPorHora);

            for (var hora = 0; hora < cantidadesPorHora.Length; hora++)
            {
                predicciones.Add(new PrediccionResultado
                {
                    Fecha = fecha,
                    HoraInicio = new TimeOnly(hora, 0),
                    AfluenciaEstimada = cantidadesPorHora[hora],
                    FechaGeneracion = generadaEn,
                    Modelo = NombreModelo
                });
            }
        }

        await repository.ReemplazarPrediccionesAsync(
            hoy.AddDays(1),
            hoy.AddDays(DiasPronosticados),
            predicciones,
            cancellationToken);
        logger.LogInformation("Se generaron {Cantidad} predicciones horarias mediante SSA.", predicciones.Count);
    }

    private static List<DailyObservation> CrearSerieDiaria(
        IReadOnlyList<HistorialAfluencia> historial,
        DateOnly hasta)
    {
        if (historial.Count == 0)
        {
            return [];
        }

        var totales = historial
            .GroupBy(registro => registro.Fecha)
            .ToDictionary(grupo => grupo.Key, grupo => grupo.Sum(registro => registro.CantidadComensales));
        var desde = historial.Min(registro => registro.Fecha);
        var dias = hasta.DayNumber - desde.DayNumber + 1;
        var serie = new List<DailyObservation>(dias);

        for (var offset = 0; offset < dias; offset++)
        {
            var fecha = desde.AddDays(offset);
            serie.Add(new DailyObservation
            {
                Total = totales.TryGetValue(fecha, out var total) ? total : 0
            });
        }

        return serie;
    }

    private static double[] CrearDistribucionPorHora(IReadOnlyList<HistorialAfluencia> historial)
    {
        var pesos = new double[24];
        foreach (var registro in historial)
        {
            pesos[registro.HoraInicio.Hour] += registro.CantidadComensales;
        }

        var suma = pesos.Sum();
        return suma == 0
            ? Enumerable.Repeat(1d / 24, 24).ToArray()
            : pesos.Select(peso => peso / suma).ToArray();
    }

    private static int[] DistribuirTotal(int total, IReadOnlyList<double> pesos)
    {
        var partes = pesos.Select(peso => total * peso).ToArray();
        var resultado = partes.Select(Math.Floor).Select(valor => (int)valor).ToArray();
        var sobrantes = total - resultado.Sum();

        foreach (var indice in Enumerable.Range(0, resultado.Length)
                     .OrderByDescending(indice => partes[indice] - resultado[indice])
                     .Take(sobrantes))
        {
            resultado[indice]++;
        }

        return resultado;
    }

    private sealed class DailyObservation
    {
        public float Total { get; set; }
    }

    private sealed class ForecastOutput
    {
        [VectorType(DiasPronosticados)]
        public float[] Forecast { get; set; } = [];
    }
}
