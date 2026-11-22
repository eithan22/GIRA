using GIRA.Application.DTOs.Analitica;
using GIRA.Application.Interfaces.Services.Analitica;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Analitica;

/// <summary>Consulta y revisión de recomendaciones gerenciales.</summary>
[ApiController]
[Route("api/analitica/recomendaciones")]
[Authorize(Policy = Politicas.VerDashboard)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public sealed class RecomendacionesController(IRecomendacionesService recomendaciones) : ControllerBase
{
    /// <summary>Obtiene recomendaciones vigentes, opcionalmente filtradas por estado.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RecomendacionDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Consultar([FromQuery] string? estado, CancellationToken ct)
    {
        if (estado is not null && estado is not ("Nueva" or "Vista" or "Descartada"))
            return BadRequest(new ProblemDetails { Status = 400, Detail = "El estado debe ser Nueva, Vista o Descartada." });
        return Ok(await recomendaciones.ConsultarAsync(estado, ct));
    }

    /// <summary>Marca una recomendación vigente como vista o descartada.</summary>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] ActualizarRecomendacionRequest request, CancellationToken ct)
    {
        if (id == Guid.Empty || request.Estado is not ("Vista" or "Descartada"))
            return BadRequest(new ProblemDetails { Status = 400, Detail = "Indica Vista o Descartada como estado." });
        return await recomendaciones.CambiarEstadoAsync(id, request.Estado, ct) ? NoContent() : NotFound();
    }
}
