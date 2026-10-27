using GIRA.Api.Common;
using GIRA.Application.DTOs.Seguridad.Accesos;
using GIRA.Application.Interfaces.Services.Seguridad.Accesos;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Seguridad;

[ApiController]
[Route("api/usuarios/{id:guid}/accesos")]
[Authorize(Policy = Politicas.GestionarUsuarios)]
public class UsuarioAccesosController : ControllerBase
{
    private readonly IHistorialAccesosService _servicio;

    /// <summary>Crea el controlador con el servicio de consulta del historial.</summary>
    public UsuarioAccesosController(IHistorialAccesosService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>
    /// Lista los intentos de inicio de sesión del usuario, del más reciente al más antiguo.
    /// Filtros opcionales: <c>exitoso</c>, <c>desde</c> y <c>hasta</c> (UTC).
    /// </summary>
    /// <param name="id">Identificador del usuario.</param>
    /// <param name="filtro">Paginación y filtros.</param>
    [HttpGet]
    [ProducesResponseType<HistorialAccesosDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Listar(Guid id, [FromQuery] HistorialAccesosRequest filtro, CancellationToken cancellationToken)
    {
        var resultado = await _servicio.ListarAsync(id, filtro, cancellationToken);
        return resultado.ToActionResult();
    }
}
