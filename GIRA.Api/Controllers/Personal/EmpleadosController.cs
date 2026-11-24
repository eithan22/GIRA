using GIRA.Api.Common;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.Interfaces.Services.Personal;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Personal;

/// <summary>
/// Empleados del personal (GI-295, GI-315, GI-316): listar, registrar, editar y activar o desactivar.
/// Solo Administrador y Gerente (<c>GestionarPersonal</c>). Los empleados no se eliminan.
/// </summary>
[ApiController]
[Route("api/empleados")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public class EmpleadosController : ControllerBase
{
    private readonly IEmpleadoService _servicio;

    /// <summary>Crea el controlador con el servicio de empleados.</summary>
    public EmpleadosController(IEmpleadoService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>Lista los empleados. Con <c>soloActivos=true</c> excluye los inactivos.</summary>
    [HttpGet]
    [Authorize(Policy = Politicas.LeerPersonal)]
    [ProducesResponseType<IReadOnlyList<EmpleadoDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar([FromQuery] bool soloActivos, CancellationToken cancellationToken) =>
        (await _servicio.ListarAsync(soloActivos, cancellationToken)).ToActionResult();

    /// <summary>Registra un empleado. Responde 409 si la cédula ya existe y 400 si el cargo no existe.</summary>
    [HttpPost]
    [Authorize(Policy = Politicas.GestionarPersonal)]
    [ProducesResponseType<EmpleadoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear([FromBody] EmpleadoRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _servicio.CrearAsync(request, cancellationToken);
        return resultado.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, resultado.Value)
            : resultado.ToActionResult();
    }

    /// <summary>Edita un empleado por su identificador.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Politicas.GestionarPersonal)]
    [ProducesResponseType<EmpleadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] EmpleadoRequest request, CancellationToken cancellationToken) =>
        (await _servicio.ActualizarAsync(id, request, cancellationToken)).ToActionResult();

    /// <summary>Activa o desactiva un empleado. No lo elimina.</summary>
    [HttpPatch("{id:guid}/estado")]
    [Authorize(Policy = Politicas.GestionarPersonal)]
    [ProducesResponseType<EmpleadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] CambiarEstadoEmpleadoRequest request, CancellationToken cancellationToken) =>
        (await _servicio.CambiarEstadoAsync(id, request.Activo, cancellationToken)).ToActionResult();
}
