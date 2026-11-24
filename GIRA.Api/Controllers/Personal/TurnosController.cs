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
    private readonly IPlanificacionService _planificacion;

    /// <summary>Crea el controlador con el servicio de turnos y el de planificación.</summary>
    public TurnosController(ITurnoService servicio, IPlanificacionService planificacion)
    {
        _servicio = servicio;
        _planificacion = planificacion;
    }

    /// <summary>
    /// Lista los turnos entre dos fechas, con filtro opcional por cargo (GI-338, RF-PER-04). Rango máximo de 31 días.
    /// Responde 400 si el rango es inválido y 404 si el cargo no existe.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<TurnoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Listar([FromQuery] DateOnly desde, [FromQuery] DateOnly hasta, [FromQuery] Guid? cargoId, CancellationToken cancellationToken) =>
        (await _servicio.ListarAsync(desde, hasta, cargoId, cancellationToken)).ToActionResult();

    /// <summary>
    /// Planificación semanal con cobertura (GI-301, RF-PER-07): por día, franja y cargo, cuántos turnos cubren la franja completa.
    /// Rango máximo de 31 días. Responde 400 si el rango es inválido y 404 si el cargo no existe.
    /// </summary>
    [HttpGet("cobertura")]
    [ProducesResponseType<IReadOnlyList<CoberturaFranjaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Cobertura([FromQuery] DateOnly desde, [FromQuery] DateOnly hasta, [FromQuery] Guid? cargoId, CancellationToken cancellationToken) =>
        (await _planificacion.CoberturaAsync(desde, hasta, cargoId, cancellationToken)).ToActionResult();

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
