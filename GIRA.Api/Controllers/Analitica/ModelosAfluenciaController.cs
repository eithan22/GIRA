using GIRA.Api.Common;
using GIRA.Application.DTOs.Analitica;
using GIRA.Application.Interfaces.Services.Analitica;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Analitica;

[ApiController]
[Route("api/analitica/modelos")]
[Authorize(Policy = Politicas.GestionarModelos)]
public sealed class ModelosAfluenciaController(IEntrenamientoModeloService servicio) : ControllerBase
{
    [HttpPost("entrenamientos")]
    [ProducesResponseType<EntrenamientoModeloSolicitadoDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> SolicitarEntrenamiento(CancellationToken cancellationToken)
    {
        var usuarioId = User.ObtenerUsuarioId();
        if (usuarioId is null)
            return Forbid();

        var id = await servicio.SolicitarAsync(usuarioId.Value, cancellationToken);
        var entrenamiento = await servicio.ConsultarAsync(id, cancellationToken)
            ?? throw new InvalidOperationException($"No se encontró el entrenamiento solicitado {id}.");
        var respuesta = new EntrenamientoModeloSolicitadoDto(id, entrenamiento.Estado);
        return AcceptedAtAction(nameof(ObtenerEntrenamiento), new { id }, respuesta);
    }

    [HttpGet("entrenamientos/{id:guid}")]
    [ProducesResponseType<EntrenamientoModeloDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerEntrenamiento(Guid id, CancellationToken cancellationToken)
    {
        var entrenamiento = await servicio.ConsultarAsync(id, cancellationToken);
        return entrenamiento is null ? NotFound() : Ok(entrenamiento);
    }

    [HttpGet]
    [ProducesResponseType<IReadOnlyList<ModeloAfluenciaDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarModelos(CancellationToken cancellationToken) =>
        Ok(await servicio.ListarModelosAsync(cancellationToken));

    [HttpPost("{modeloId:guid}/activar")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> ActivarModelo(
        Guid modeloId, [FromBody] ActivarModeloRequest solicitud, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(solicitud.Motivo) || solicitud.Motivo.Trim().Length > 500)
        {
            return BadRequest(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Detail = "Indica un motivo de hasta 500 caracteres para auditar la activación."
            });
        }

        var usuarioId = User.ObtenerUsuarioId();
        if (usuarioId is null)
            return Forbid();
        var resultado = await servicio.ActivarModeloAsync(
            modeloId, usuarioId.Value, solicitud.Motivo, cancellationToken);
        return resultado switch
        {
            ResultadoActivacionModelo.Activado or ResultadoActivacionModelo.YaActivo => NoContent(),
            ResultadoActivacionModelo.NoEncontrado => NotFound(),
            ResultadoActivacionModelo.NoEsRollback => Conflict(new ProblemDetails
            {
                Status = StatusCodes.Status409Conflict,
                Detail = "Solo se puede reactivar una versión anterior a la vigente."
            }),
            _ => throw new InvalidOperationException("Resultado de activación de modelo no reconocido.")
        };
    }
}
