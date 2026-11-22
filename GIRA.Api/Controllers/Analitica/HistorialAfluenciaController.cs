using GIRA.Application.DTOs.Analitica;
using GIRA.Application.Interfaces.Services.Analitica;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Analitica;

/// <summary>Consulta la afluencia registrada por fecha y franja.</summary>
[ApiController]
[Route("api/analitica/historial")]
[Authorize(Policy = Politicas.VerDashboard)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class HistorialAfluenciaController(IHistorialAfluenciaService historial) : ControllerBase
{
    /// <summary>Obtiene los registros reales del intervalo, incluidos ambos extremos.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<HistorialAfluenciaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Consultar([FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta, CancellationToken ct)
    {
        if (desde is null || hasta is null || desde.Value == default || hasta.Value == default || desde > hasta)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Detail = "Indica fechas válidas y asegúrate de que desde no sea posterior a hasta."
            });
        }

        return Ok(await historial.ConsultarAsync(desde.Value, hasta.Value, ct));
    }
}
