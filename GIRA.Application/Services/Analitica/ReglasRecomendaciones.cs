using GIRA.Application.DTOs.Analitica;

namespace GIRA.Application.Services.Analitica;

public sealed record AlertaRecurrente(Guid InsumoId, string Insumo, int Cantidad);

public sealed record RecomendacionCalculada(
    string Clave, DateOnly Fecha, string Tipo, string Mensaje, string Prioridad, string Origen);

/// <summary>Aplica RN-ANA-06 sin convertir observaciones ausentes en cero.</summary>
public static class ReglasRecomendaciones
{
    public static IReadOnlyList<RecomendacionCalculada> Calcular(
        DateOnly hoy, IReadOnlyList<HistorialAfluenciaDto> historial,
        IReadOnlyList<PrediccionAfluenciaDto> predicciones, IReadOnlyList<AlertaRecurrente> alertas,
        OpcionesRecomendaciones opciones)
    {
        var resultado = new List<RecomendacionCalculada>();
        var promedios = historial.Where(r => r.Fecha >= hoy.AddDays(-27) && r.Fecha <= hoy &&
                (r.ReservasCompletadas > 0 || r.ComensalesSinReserva > 0))
            .GroupBy(r => (r.FranjaHorariaId, r.Fecha.DayOfWeek))
            .ToDictionary(g => g.Key, g => g.Average(r => (decimal)r.TotalComensales));

        foreach (var prediccion in predicciones.Where(p => p.Fecha >= hoy && p.Fecha <= hoy.AddDays(14)))
        {
            var clave = $"{prediccion.Fecha:yyyy-MM-dd}:{prediccion.FranjaHorariaId:D}";
            if (promedios.TryGetValue((prediccion.FranjaHorariaId, prediccion.Fecha.DayOfWeek), out var promedio)
                && promedio > 0 && prediccion.AfluenciaEstimada >= promedio * (1 + opciones.PorcentajeAumentoDemanda / 100m))
            {
                resultado.Add(new($"demanda:{clave}", prediccion.Fecha, "Personal",
                    $"Demanda elevada para {prediccion.Franja} el {prediccion.Fecha:dd/MM/yyyy}. Refuerza el personal y revisa el inventario.",
                    "Alta", "Predicción"));
            }

            if (prediccion.BajaConfianza)
            {
                resultado.Add(new($"confianza:{clave}", prediccion.Fecha, "Analítica",
                    $"La predicción de {prediccion.Franja} para el {prediccion.Fecha:dd/MM/yyyy} tiene baja confianza; se necesitan más datos.",
                    "Media", "Predicción"));
            }
        }

        var semana = historial.Where(r => r.Fecha >= hoy.AddDays(-6) && r.Fecha <= hoy).ToList();
        var completadas = semana.Sum(r => r.ReservasCompletadas);
        var ausencias = semana.Sum(r => r.ReservasNoAsistidas);
        if (completadas + ausencias > 0 && ausencias * 100m / (completadas + ausencias) > opciones.PorcentajeNoShow)
        {
            resultado.Add(new($"no-show:{hoy:yyyy-MM-dd}", hoy, "Reservas",
                "La tasa de no asistencia de los últimos 7 días supera el umbral. Confirma las reservas por teléfono.",
                "Media", "Historial"));
        }

        foreach (var alerta in alertas.Where(a => a.Cantidad > opciones.AlertasMensuales))
        {
            resultado.Add(new($"alertas:{hoy:yyyy-MM}:{alerta.InsumoId:D}", hoy, "Inventario",
                $"{alerta.Insumo} acumula {alerta.Cantidad} alertas este mes. Revisa si conviene subir su punto de reorden.",
                "Media", "Inventario"));
        }

        return resultado;
    }
}
