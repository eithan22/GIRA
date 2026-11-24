using GIRA.Api.Common;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.Interfaces.Services.Personal;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Personal;

/// <summary>
/// Ausencias de los empleados (GI-305, GI-355, GI-356, RF-PER-11): listar, registrar y aprobar o rechazar.
/// Por ahora solo Administrador y Gerente (<c>GestionarPersonal</c>). Que el propio empleado pida su ausencia
/// y vea solo las suyas depende del vínculo usuario-empleado (GI-133) y de la política "Staff" del SRS.
/// </summary>
[ApiController]
[Route("api/ausencias")]
[Authorize(Policy = Politicas.GestionarPersonal)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public class AusenciasController : ControllerBase
{
    private readonly IAusenciaService _servicio;

    /// <summary>Crea el controlador con el servicio de ausencias.</summary>
    public AusenciasController(IAusenciaService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>Lista las ausencias. Con <c>empleadoId</c> solo las de ese empleado.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<AusenciaDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar([FromQuery] Guid? empleadoId, CancellationToken cancellationToken) =>
        (await _servicio.ListarAsync(empleadoId, cancellationToken)).ToActionResult();

    /// <summary>Registra una solicitud de ausencia en estado Solicitada.</summary>
    [HttpPost]
    [ProducesResponseType<AusenciaDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Crear([FromBody] AusenciaRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _servicio.CrearAsync(request, cancellationToken);
        return resultado.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, resultado.Value)
            : resultado.ToActionResult();
    }

    /// <summary>Aprueba o rechaza una solicitud pendiente. Responde 409 si ya fue resuelta.</summary>
    [HttpPatch("{id:guid}/estado")]
    [ProducesResponseType<AusenciaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Resolver(Guid id, [FromBody] CambiarEstadoAusenciaRequest request, CancellationToken cancellationToken)
    {
        var usuario = User.ObtenerUsuarioId();
        if (usuario is null)
        {
            return Unauthorized();
        }

        return (await _servicio.ResolverAsync(id, request.Estado, usuario.Value, cancellationToken)).ToActionResult();
    }
}
