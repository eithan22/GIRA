using GIRA.Application.DTOs.Analitica;
using GIRA.Application.Interfaces.Services.Analitica;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Analitica;

/// <summary>Consulta predicciones vigentes de afluencia.</summary>
[ApiController]
[Route("api/analitica/prediccion")]
[Authorize(Policy = Politicas.VerPrediccion)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class PrediccionAfluenciaController(IPrediccionAfluenciaService predicciones) : ControllerBase
{
    /// <summary>Obtiene predicciones vigentes para un rango inclusivo y una franja opcional.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<PrediccionAfluenciaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Consultar(
        [FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta, [FromQuery] Guid? franjaId, CancellationToken ct)
    {
        if (desde is null || hasta is null || desde.Value == default || desde > hasta ||
            hasta.Value.DayNumber - desde.Value.DayNumber > 365 || franjaId == Guid.Empty)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Detail = "Indica un rango válido de hasta 366 días y una franja válida."
            });
        }

        return Ok(await predicciones.ConsultarAsync(desde.Value, hasta.Value, franjaId, ct));
    }
}
