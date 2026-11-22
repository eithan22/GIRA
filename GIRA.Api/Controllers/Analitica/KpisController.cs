using GIRA.Application.DTOs.Analitica;
using GIRA.Application.Interfaces.Services.Analitica;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Analitica;

/// <summary>Indicadores del dashboard gerencial.</summary>
[ApiController]
[Route("api/analitica/kpis")]
[Authorize(Policy = Politicas.VerDashboard)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class KpisController(IKpisService kpis) : ControllerBase
{
    /// <summary>Consulta los indicadores para un intervalo inclusivo de fechas locales.</summary>
    [HttpGet]
    [ProducesResponseType<KpisDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Consultar([FromQuery] DateOnly? desde, [FromQuery] DateOnly? hasta, CancellationToken ct)
    {
        if (desde is null || hasta is null || desde.Value == default || hasta == DateOnly.MaxValue || desde > hasta)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Detail = "Indica fechas válidas y asegúrate de que desde no sea posterior a hasta."
            });
        }

        return Ok(await kpis.ConsultarAsync(desde.Value, hasta.Value, ct));
    }
}
