using GIRA.Application.DTOs.Analitica;
using GIRA.Application.Interfaces.Services.Analitica;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Analitica;

/// <summary>Expone el historial, las predicciones y los KPIs de Analítica.</summary>
[ApiController]
[Route("api/analitica")]
public sealed class AnaliticaController(IAnaliticaService analiticaService) : ControllerBase
{
    /// <summary>Obtiene las predicciones horarias para una fecha.</summary>
    /// <param name="fecha">Día para el que se solicitan predicciones.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    [HttpGet("prediccion")]
    [ProducesResponseType<IReadOnlyList<PrediccionAfluenciaDto>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<PrediccionAfluenciaDto>>> ObtenerPrediccion(
        [FromQuery] DateOnly fecha,
        CancellationToken cancellationToken)
    {
        var predicciones = await analiticaService.ObtenerPrediccionesAsync(fecha, cancellationToken);
        return Ok(predicciones);
    }

    /// <summary>Obtiene los indicadores gerenciales de los últimos 30 días.</summary>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    [HttpGet("kpis")]
    [ProducesResponseType<KpisAnaliticaDto>(StatusCodes.Status200OK)]
    public async Task<ActionResult<KpisAnaliticaDto>> ObtenerKpis(CancellationToken cancellationToken)
    {
        var kpis = await analiticaService.ObtenerKpisAsync(cancellationToken);
        return Ok(kpis);
    }

    /// <summary>Consulta el historial de afluencia en un rango de fechas inclusivo.</summary>
    /// <param name="desde">Primer día del rango.</param>
    /// <param name="hasta">Último día del rango.</param>
    /// <param name="cancellationToken">Token de cancelación de la solicitud.</param>
    [HttpGet("historial")]
    [ProducesResponseType<IReadOnlyList<HistorialAfluenciaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<HistorialAfluenciaDto>>> ObtenerHistorial(
        [FromQuery] DateOnly desde,
        [FromQuery] DateOnly hasta,
        CancellationToken cancellationToken)
    {
        if (desde > hasta || hasta.DayNumber - desde.DayNumber > 365)
        {
            return BadRequest(new ProblemDetails
            {
                Title = "Rango de fechas inválido",
                Detail = "El rango debe ser ascendente y no puede superar 366 días.",
                Status = StatusCodes.Status400BadRequest
            });
        }

        var historial = await analiticaService.ObtenerHistorialAsync(desde, hasta, cancellationToken);
        return Ok(historial);
    }
}
