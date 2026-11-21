using GIRA.Api.Common;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.Interfaces.Services.Personal;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Personal;

/// <summary>Registro de entrada y salida de los turnos propios.</summary>
[ApiController]
[Route("api/personal/marcaciones")]
[Authorize(Policy = Politicas.ConsultarMisTurnos)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public sealed class MarcacionesController(IMarcacionTurnoService servicio) : ControllerBase
{
    /// <summary>Consulta el turno abierto del usuario autenticado.</summary>
    [HttpGet("actual")]
    [ProducesResponseType<MarcacionTurnoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerActual(CancellationToken cancellationToken)
    {
        var usuarioId = User.ObtenerUsuarioId();
        if (usuarioId is null)
            return Forbid();

        var turno = await servicio.ObtenerAbiertaAsync(usuarioId.Value, cancellationToken);
        return turno is null ? NotFound() : Ok(turno);
    }

    /// <summary>Registra la entrada del usuario autenticado.</summary>
    [HttpPost("entrada")]
    [ProducesResponseType<MarcacionTurnoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> MarcarEntrada(CancellationToken cancellationToken)
    {
        var usuarioId = User.ObtenerUsuarioId();
        if (usuarioId is null)
            return Forbid();

        var resultado = await servicio.MarcarEntradaAsync(usuarioId.Value, cancellationToken);
        if (resultado.IsFailure)
            return resultado.ToActionResult();
        return CreatedAtAction(nameof(ObtenerActual), null, resultado.Value);
    }

    /// <summary>Cierra el turno abierto del usuario e informa los minutos de pausa.</summary>
    [HttpPost("salida")]
    [ProducesResponseType<MarcacionTurnoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarcarSalida(
        [FromBody] FinalizarTurnoRequest request, CancellationToken cancellationToken)
    {
        var usuarioId = User.ObtenerUsuarioId();
        if (usuarioId is null)
            return Forbid();

        return (await servicio.MarcarSalidaAsync(usuarioId.Value, request.MinutosPausa, cancellationToken))
            .ToActionResult();
    }
}
