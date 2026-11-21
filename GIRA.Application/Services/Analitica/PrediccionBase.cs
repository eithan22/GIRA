using GIRA.Application.DTOs.Analitica;
using GIRA.Domain.Entities.Analitica;

namespace GIRA.Application.Services.Analitica;

/// <summary>Pronóstico de baja confianza basado en fechas observadas del mismo día de la semana.</summary>
public static class PrediccionBase
{
    public const int HorizonteDias = 14;
    public const string Version = "promedio-semanal-v1";

    public static IReadOnlyList<PrediccionAfluencia> Generar(
        DateOnly hoy, DateTimeOffset generadaEn, Guid generacionId,
        IReadOnlyCollection<Guid> franjasActivas, IReadOnlyList<HistorialAfluenciaDto> historial)
    {
        var inicio = hoy.AddDays(-27);
        var franjas = franjasActivas.ToHashSet();
        var observaciones = historial
            .Where(r => r.Fecha >= inicio && r.Fecha <= hoy && franjas.Contains(r.FranjaHorariaId)
                && (r.ReservasCompletadas > 0 || r.ComensalesSinReserva > 0))
            .GroupBy(r => new { r.FranjaHorariaId, r.Fecha.DayOfWeek })
            .ToDictionary(g => g.Key, g => g.Average(r => (decimal)r.TotalComensales));

        var resultado = new List<PrediccionAfluencia>();
        for (var i = 1; i <= HorizonteDias; i++)
        {
            var fecha = hoy.AddDays(i);
            foreach (var franja in franjas)
            {
                if (!observaciones.TryGetValue(new { FranjaHorariaId = franja, fecha.DayOfWeek }, out var promedio))
                    continue;

                resultado.Add(new PrediccionAfluencia
                {
                    Id = Guid.NewGuid(),
                    GeneracionId = generacionId,
                    Fecha = fecha,
                    FranjaHorariaId = franja,
                    AfluenciaEstimada = decimal.ToInt32(decimal.Round(promedio, 0, MidpointRounding.AwayFromZero)),
                    BajaConfianza = true,
                    GeneradaEn = generadaEn,
                    VersionModelo = Version,
                    Vigente = true
                });
            }
        }

        return resultado;
    }
}
