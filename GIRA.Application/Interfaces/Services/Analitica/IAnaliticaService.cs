using GIRA.Application.DTOs.Analitica;

namespace GIRA.Application.Interfaces.Services.Analitica;

/// <summary>Casos de uso disponibles para el dashboard de Analítica.</summary>
public interface IAnaliticaService
{
    /// <summary>Registra la afluencia asociada a una reserva confirmada.</summary>
    Task RegistrarReservaConfirmadaAsync(
        Guid reservaId,
        DateOnly fecha,
        TimeOnly horaInicio,
        int cantidadComensales,
        CancellationToken cancellationToken);

    /// <summary>Consulta el historial dentro de un rango inclusivo de fechas.</summary>
    Task<IReadOnlyList<HistorialAfluenciaDto>> ObtenerHistorialAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken cancellationToken);

    /// <summary>Consulta la predicción calculada para una fecha.</summary>
    Task<IReadOnlyList<PrediccionAfluenciaDto>> ObtenerPrediccionesAsync(
        DateOnly fecha,
        CancellationToken cancellationToken);

    /// <summary>Calcula los indicadores de los últimos 30 días.</summary>
    Task<KpisAnaliticaDto> ObtenerKpisAsync(CancellationToken cancellationToken);
}
