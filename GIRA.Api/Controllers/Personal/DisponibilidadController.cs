using GIRA.Api.Common;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.Interfaces.Services.Personal;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Personal;

/// <summary>
/// Disponibilidad semanal de un empleado (GI-297, GI-320, RF-PER-03): listar, registrar y eliminar bloques.
/// Solo Administrador y Gerente (<c>GestionarPersonal</c>). Los bloques de un mismo día no pueden solaparse.
/// </summary>
[ApiController]
[Route("api/empleados/{empleadoId:guid}/disponibilidad")]
[Authorize(Policy = Politicas.GestionarPersonal)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public class DisponibilidadController : ControllerBase
{
    private readonly IDisponibilidadService _servicio;

    /// <summary>Crea el controlador con el servicio de disponibilidad.</summary>
    public DisponibilidadController(IDisponibilidadService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>Lista los bloques de disponibilidad del empleado.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<DisponibilidadDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Listar(Guid empleadoId, CancellationToken cancellationToken) =>
        (await _servicio.ListarAsync(empleadoId, cancellationToken)).ToActionResult();

    /// <summary>Registra un bloque. Responde 409 si se solapa con otro bloque del mismo día.</summary>
    [HttpPost]
    [ProducesResponseType<DisponibilidadDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Agregar(Guid empleadoId, [FromBody] DisponibilidadRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _servicio.AgregarAsync(empleadoId, request, cancellationToken);
        return resultado.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, resultado.Value)
            : resultado.ToActionResult();
    }

    /// <summary>Elimina un bloque (borrado lógico).</summary>
    [HttpDelete("{id:guid}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Eliminar(Guid empleadoId, Guid id, CancellationToken cancellationToken) =>
        (await _servicio.EliminarAsync(empleadoId, id, cancellationToken)).ToActionResult();
}
