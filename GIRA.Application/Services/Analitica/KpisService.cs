using GIRA.Application.Common;
using GIRA.Application.DTOs.Analitica;
using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Application.Interfaces.Services.Analitica;

namespace GIRA.Application.Services.Analitica;

/// <summary>Calcula indicadores con los datos disponibles, sin inventar datos ausentes.</summary>
public sealed class KpisService(IHistorialAfluenciaService historial, IKpisRepository datos) : IKpisService
{
    public async Task<KpisDto> ConsultarAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken)
    {
        var registros = await historial.ConsultarAsync(desde, hasta, cancellationToken);
        var desdeUtc = TimeZoneInfo.ConvertTimeToUtc(desde.ToDateTime(TimeOnly.MinValue), HoraLocalGira.ZonaHoraria);
        var hastaUtc = TimeZoneInfo.ConvertTimeToUtc(hasta.AddDays(1).ToDateTime(TimeOnly.MinValue), HoraLocalGira.ZonaHoraria);
        var reservas = await datos.ContarReservasAsync(desdeUtc, hastaUtc, cancellationToken);
        var alertas = await datos.ContarAlertasAbiertasAsync(cancellationToken);
        var precision = await datos.CalcularPrecisionModeloAsync(desde, hasta, cancellationToken);
        var capacidad = await datos.ConsultarCapacidadAsync(desde, hasta, cancellationToken);
        var costos = await datos.ConsultarCostosAsync(desdeUtc, hastaUtc, cancellationToken);
        var horasPersonal = await datos.ConsultarHorasPersonalAsync(desdeUtc, hastaUtc, cancellationToken);
        var franjas = await datos.ObtenerFranjasAsync(registros.Select(r => r.FranjaHorariaId).Distinct().ToList(), cancellationToken);

        var comensales = registros.Sum(r => r.TotalComensales);
        var sinReserva = registros.Sum(r => r.ComensalesSinReserva);
        var completadas = registros.Sum(r => r.ReservasCompletadas);
        var noAsistidas = registros.Sum(r => r.ReservasNoAsistidas);
        var promedio = registros
            .GroupBy(r => new { r.Fecha.DayOfWeek, r.FranjaHorariaId })
            .OrderBy(g => g.Key.DayOfWeek).ThenBy(g => g.Key.FranjaHorariaId)
            .Select(g => new AfluenciaPromedioDto(g.Key.DayOfWeek, g.Key.FranjaHorariaId,
                franjas.TryGetValue(g.Key.FranjaHorariaId, out var nombre) ? nombre : g.Key.FranjaHorariaId.ToString(),
                g.Average(r => (decimal)r.TotalComensales), g.Count()))
            .ToList();

        var diasDelPeriodo = hasta.DayNumber - desde.DayNumber + 1;
        decimal? ocupacion = registros.Count == 0 || capacidad.DiasRegistrados != diasDelPeriodo ||
            capacidad.CapacidadTotal == 0
                ? null
                : comensales * 100m / capacidad.CapacidadTotal;
        decimal? costoPorComensal = comensales <= 0 ||
            costos.CostoSalidas is null || costos.CostoMermas is null
                ? null
                : (costos.CostoSalidas.Value + costos.CostoMermas.Value) / comensales;
        var costoSalidasTotales = (costos.CostoSalidas ?? 0) + (costos.CostoMermas ?? 0);
        decimal? merma = costos.CostoSalidas is null || costos.CostoMermas is null ||
            costoSalidasTotales == 0
                ? null
                : costos.CostoMermas.Value * 100m / costoSalidasTotales;
        decimal? horasPorComensal = horasPersonal is null || comensales <= 0
            ? null
            : horasPersonal.Value / comensales;

        return new KpisDto(desde, hasta, registros.Count == 0 ? null : comensales, ocupacion,
            Porcentaje(noAsistidas, completadas + noAsistidas),
            Porcentaje(reservas.Canceladas, reservas.Creadas), promedio,
            Porcentaje(sinReserva, comensales), precision, costoPorComensal, merma, alertas, horasPorComensal);
    }

    private static decimal? Porcentaje(int numerador, int denominador) =>
        denominador == 0 ? null : numerador * 100m / denominador;
}
