using GIRA.Api.Common;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.Interfaces.Services.Personal;
using GIRA.Application.Interfaces.Services.Seguridad.Usuarios;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Personal;

/// <summary>
/// Disponibilidad y turnos propios del empleado que inició sesión (GI-321 y GI-300). El empleado se obtiene del
/// usuario autenticado, nunca de un parámetro, así que nadie puede consultar o cambiar los datos de otro (RN-PER-09).
/// Cualquier rol del personal puede usar estas rutas (<c>ConsultarMisTurnos</c>), pero el usuario debe estar vinculado a un empleado.
/// </summary>
[ApiController]
[Route("api/mi")]
[Authorize(Policy = Politicas.ConsultarMisTurnos)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public class MiPersonalController : ControllerBase
{
    private readonly IDisponibilidadService _disponibilidad;
    private readonly ITurnoService _turnos;
    private readonly IUsuarioService _usuarios;

    /// <summary>Crea el controlador con los servicios de disponibilidad, turnos y usuarios.</summary>
    public MiPersonalController(IDisponibilidadService disponibilidad, ITurnoService turnos, IUsuarioService usuarios)
    {
        _disponibilidad = disponibilidad;
        _turnos = turnos;
        _usuarios = usuarios;
    }

    /// <summary>Lista mi disponibilidad semanal (GI-321).</summary>
    [HttpGet("disponibilidad")]
    [ProducesResponseType<IReadOnlyList<DisponibilidadDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListarDisponibilidad(CancellationToken cancellationToken)
    {
        var (empleadoId, error) = await ObtenerEmpleadoAsync(cancellationToken);
        if (error is not null) return error;
        return (await _disponibilidad.ListarAsync(empleadoId!.Value, cancellationToken)).ToActionResult();
    }

    /// <summary>Registra un bloque de mi disponibilidad. Responde 409 si se solapa con otro bloque del mismo día (GI-321).</summary>
    [HttpPost("disponibilidad")]
    [ProducesResponseType<DisponibilidadDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> AgregarDisponibilidad([FromBody] DisponibilidadRequest request, CancellationToken cancellationToken)
    {
        var (empleadoId, error) = await ObtenerEmpleadoAsync(cancellationToken);
        if (error is not null) return error;

        var resultado = await _disponibilidad.AgregarAsync(empleadoId!.Value, request, cancellationToken);
        return resultado.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, resultado.Value)
            : resultado.ToActionResult();
    }

    /// <summary>Quita un bloque de mi disponibilidad (borrado lógico). Solo puedo quitar los míos (GI-321).</summary>
    [HttpDelete("disponibilidad/{id:guid}")]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> EliminarDisponibilidad(Guid id, CancellationToken cancellationToken)
    {
        var (empleadoId, error) = await ObtenerEmpleadoAsync(cancellationToken);
        if (error is not null) return error;
        return (await _disponibilidad.EliminarAsync(empleadoId!.Value, id, cancellationToken)).ToActionResult();
    }

    /// <summary>Lista solo mis turnos entre dos fechas (GI-300). Rango máximo de 31 días.</summary>
    [HttpGet("turnos")]
    [ProducesResponseType<IReadOnlyList<TurnoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ListarTurnos([FromQuery] DateOnly desde, [FromQuery] DateOnly hasta, CancellationToken cancellationToken)
    {
        var (empleadoId, error) = await ObtenerEmpleadoAsync(cancellationToken);
        if (error is not null) return error;
        return (await _turnos.ListarDeEmpleadoAsync(empleadoId!.Value, desde, hasta, cancellationToken)).ToActionResult();
    }

    /// <summary>
    /// Obtiene el empleado vinculado al usuario autenticado. Si el usuario no tiene empleado, responde 404 con un mensaje claro.
    /// </summary>
    private async Task<(Guid? EmpleadoId, IActionResult? Error)> ObtenerEmpleadoAsync(CancellationToken cancellationToken)
    {
        var usuarioId = User.ObtenerUsuarioId();
        if (usuarioId is null)
            return (null, Unauthorized());

        var actual = await _usuarios.ObtenerActualAsync(usuarioId.Value, cancellationToken);
        if (actual.IsFailure)
            return (null, actual.ToActionResult());

        if (actual.Value!.EmpleadoId is null)
            return (null, OperationResult.Failure<bool>(ErrorType.NotFound, "Tu usuario no está vinculado a un empleado.").ToActionResult());

        return (actual.Value.EmpleadoId, null);
    }
}
