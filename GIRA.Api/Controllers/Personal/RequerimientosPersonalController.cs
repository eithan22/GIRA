using GIRA.Api.Common;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.Interfaces.Services.Personal;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Personal;

/// <summary>
/// Requerimiento de personal por cargo (GI-302, GI-343, RF-PER-08): listar y configurar. Hay un solo
/// requerimiento por cargo, así que el PUT crea o actualiza según el cargo indicado en el cuerpo.
/// Solo Administrador y Gerente (<c>GestionarPersonal</c>).
/// </summary>
[ApiController]
[Route("api/requerimientos-personal")]
[Authorize(Policy = Politicas.GestionarPersonal)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public class RequerimientosPersonalController : ControllerBase
{
    private readonly IRequerimientoPersonalService _servicio;

    /// <summary>Crea el controlador con el servicio de requerimientos.</summary>
    public RequerimientosPersonalController(IRequerimientoPersonalService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>Lista los requerimientos configurados por cargo.</summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<RequerimientoPersonalDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken) =>
        (await _servicio.ListarAsync(cancellationToken)).ToActionResult();

    /// <summary>Crea o actualiza el requerimiento del cargo indicado. Responde 400 si los valores no son mayores que cero o el cargo no existe.</summary>
    [HttpPut]
    [ProducesResponseType<RequerimientoPersonalDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Guardar([FromBody] RequerimientoPersonalRequest request, CancellationToken cancellationToken) =>
        (await _servicio.GuardarAsync(request, cancellationToken)).ToActionResult();
}
