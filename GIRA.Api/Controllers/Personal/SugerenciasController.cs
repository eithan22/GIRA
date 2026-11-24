using GIRA.Api.Common;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.Interfaces.Services.Personal;
using GIRA.Domain.Constants;
using GIRA.Domain.Enums.Personal;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Personal;

/// <summary>
/// Sugerencias de personal (GI-304, RF-PER-10): listar y aceptar o rechazar. Solo Administrador, Gerente y Supervisor
/// (<c>AsignarTurnos</c>). Las sugerencias nunca cambian turnos por sí solas (RN-PER-06).
/// </summary>
[ApiController]
[Route("api/turnos/sugerencias")]
[Authorize(Policy = Politicas.AsignarTurnos)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public class SugerenciasController : ControllerBase
{
    private readonly ISugerenciaService _servicio;

    /// <summary>Crea el controlador con el servicio de sugerencias.</summary>
    public SugerenciasController(ISugerenciaService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>Lista las sugerencias, opcionalmente filtradas por estado (Pendiente, Aceptada, Rechazada, Vencida).</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<SugerenciaDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar([FromQuery] EstadoSugerencia? estado, CancellationToken cancellationToken) =>
        (await _servicio.ListarAsync(estado, cancellationToken)).ToActionResult();

    /// <summary>Acepta o rechaza una sugerencia pendiente. Responde 409 si ya fue resuelta y 400 si el estado pedido no es Aceptada ni Rechazada.</summary>
    [HttpPatch("{id:guid}")]
    [ProducesResponseType<SugerenciaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Resolver(Guid id, [FromBody] ResolverSugerenciaRequest request, CancellationToken cancellationToken)
    {
        var usuarioId = User.ObtenerUsuarioId();
        if (usuarioId is null)
        {
            return Unauthorized();
        }

        return (await _servicio.ResolverAsync(id, request.Estado, usuarioId.Value, cancellationToken)).ToActionResult();
    }
}
