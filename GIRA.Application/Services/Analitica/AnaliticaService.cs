using GIRA.Application.DTOs.Analitica;
using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Application.Interfaces.Services.Analitica;
using GIRA.Domain.Entities.Analitica;

namespace GIRA.Application.Services.Analitica;

/// <summary>Implementa las consultas y reglas de aplicación del dashboard gerencial.</summary>
public sealed class AnaliticaService(IAnaliticaRepository repository) : IAnaliticaService
{
    /// <inheritdoc />
    public async Task RegistrarReservaConfirmadaAsync(
        Guid reservaId,
        DateOnly fecha,
        TimeOnly horaInicio,
        int cantidadComensales,
        CancellationToken cancellationToken)
    {
        if (reservaId == Guid.Empty)
        {
            throw new ArgumentException("La reserva debe tener un identificador válido.", nameof(reservaId));
        }

        if (cantidadComensales <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cantidadComensales),
                "La cantidad de comensales debe ser mayor que cero.");
        }

        if (await repository.ExisteReservaAsync(reservaId, cancellationToken))
        {
            return;
        }

        await repository.AgregarHistorialAsync(new HistorialAfluencia
        {
            ReservaId = reservaId,
            Fecha = fecha,
            HoraInicio = horaInicio,
            CantidadComensales = cantidadComensales
        }, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HistorialAfluenciaDto>> ObtenerHistorialAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken cancellationToken)
    {
        ValidarRango(desde, hasta);
        var historial = await repository.ObtenerHistorialAsync(desde, hasta, cancellationToken);
        return historial
            .Select(registro => new HistorialAfluenciaDto(
                registro.Fecha,
                registro.HoraInicio,
                registro.CantidadComensales))
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<PrediccionAfluenciaDto>> ObtenerPrediccionesAsync(
        DateOnly fecha,
        CancellationToken cancellationToken)
    {
        var predicciones = await repository.ObtenerPrediccionesAsync(fecha, cancellationToken);
        return predicciones
            .Select(prediccion => new PrediccionAfluenciaDto(
                prediccion.Fecha,
                prediccion.HoraInicio,
                prediccion.AfluenciaEstimada,
                DateTime.SpecifyKind(prediccion.FechaGeneracion, DateTimeKind.Utc),
                prediccion.Modelo))
            .ToArray();
    }

    /// <inheritdoc />
    public async Task<KpisAnaliticaDto> ObtenerKpisAsync(CancellationToken cancellationToken)
    {
        var hoy = DateOnly.FromDateTime(DateTime.UtcNow);
        var desde = hoy.AddDays(-29);
        var anteriorDesde = desde.AddDays(-30);
        var anteriorHasta = desde.AddDays(-1);

        var historial = await repository.ObtenerHistorialAsync(desde, hoy, cancellationToken);
        var historialAnterior = await repository.ObtenerHistorialAsync(anteriorDesde, anteriorHasta, cancellationToken);
        var manana = await repository.ObtenerPrediccionesAsync(hoy.AddDays(1), cancellationToken);

        var totalActual = historial.Sum(registro => registro.CantidadComensales);
        var totalAnterior = historialAnterior.Sum(registro => registro.CantidadComensales);
        decimal? variacion = totalAnterior == 0
            ? null
            : decimal.Round((totalActual - totalAnterior) * 100m / totalAnterior, 1);

        return new KpisAnaliticaDto(
            desde,
            hoy,
            totalActual,
            decimal.Round(totalActual / 30m, 1),
            manana.Count == 0 ? null : manana.Sum(prediccion => prediccion.AfluenciaEstimada),
            variacion,
            historial.Where(registro => registro.CantidadComensales > 0)
                .Select(registro => registro.Fecha)
                .Distinct()
                .Count());
    }

    private static void ValidarRango(DateOnly desde, DateOnly hasta)
    {
        if (desde > hasta)
        {
            throw new ArgumentException("La fecha inicial debe ser anterior o igual a la fecha final.");
        }

        if (hasta.DayNumber - desde.DayNumber > 365)
        {
            throw new ArgumentOutOfRangeException(nameof(hasta), "El rango de consulta no puede superar 366 días.");
        }
    }
}
