using GIRA.Api.Common;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.Interfaces.Services.Personal;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Personal;

/// <summary>
/// Turnos del personal (GI-298, GI-327, GI-328, RF-PER-04): asignar, modificar y cancelar.
/// Solo Administrador, Gerente y Supervisor (<c>AsignarTurnos</c>). Los errores salen como ProblemDetails.
/// </summary>
[ApiController]
[Route("api/turnos")]
[Authorize(Policy = Politicas.AsignarTurnos)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public class TurnosController : ControllerBase
{
    private readonly ITurnoService _servicio;

    /// <summary>Crea el controlador con el servicio de turnos.</summary>
    public TurnosController(ITurnoService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>Asigna un turno. Responde 409 si hay solapamiento o ausencia aprobada, y 400 si está fuera de disponibilidad.</summary>
    [HttpPost]
    [ProducesResponseType<TurnoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear([FromBody] TurnoRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _servicio.CrearAsync(request, cancellationToken);
        return resultado.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, resultado.Value)
            : resultado.ToActionResult();
    }

    /// <summary>Modifica un turno vigente. Los turnos pasados o cancelados responden 409.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<TurnoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] TurnoRequest request, CancellationToken cancellationToken) =>
        (await _servicio.ActualizarAsync(id, request, cancellationToken)).ToActionResult();

    /// <summary>Cambia el estado de un turno: Cancelado, o Completado y Ausente si el turno ya pasó (GI-307).</summary>
    [HttpPatch("{id:guid}/estado")]
    [ProducesResponseType<TurnoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] CambiarEstadoTurnoRequest request, CancellationToken cancellationToken) =>
        (await _servicio.CambiarEstadoAsync(id, request.Estado, cancellationToken)).ToActionResult();
}
