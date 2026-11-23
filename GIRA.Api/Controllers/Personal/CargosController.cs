using GIRA.Api.Common;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.Interfaces.Services.Personal;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Personal;

/// <summary>
/// Cargos del personal (GI-311, RF-PER-02): listar, crear y editar. Solo Administrador y Gerente
/// (<c>GestionarPersonal</c>). Los errores salen como ProblemDetails.
/// </summary>
[ApiController]
[Route("api/cargos")]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public class CargosController : ControllerBase
{
    private readonly ICargoService _servicio;

    /// <summary>Crea el controlador con el servicio de cargos.</summary>
    public CargosController(ICargoService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>Lista los cargos ordenados por nombre.</summary>
    [HttpGet]
    [Authorize(Policy = Politicas.LeerPersonal)]
    [ProducesResponseType<IReadOnlyList<CargoDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken) =>
        (await _servicio.ListarAsync(cancellationToken)).ToActionResult();

    /// <summary>Crea un cargo. Responde 409 si el nombre ya existe.</summary>
    [HttpPost]
    [Authorize(Policy = Politicas.GestionarPersonal)]
    [ProducesResponseType<CargoDto>(StatusCodes.Status201Created)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear([FromBody] CargoRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _servicio.CrearAsync(request, cancellationToken);
        return resultado.IsSuccess
            ? StatusCode(StatusCodes.Status201Created, resultado.Value)
            : resultado.ToActionResult();
    }

    /// <summary>Edita un cargo por su identificador. Responde 404 si no existe y 409 si el nombre ya lo usa otro.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Politicas.GestionarPersonal)]
    [ProducesResponseType<CargoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] CargoRequest request, CancellationToken cancellationToken) =>
        (await _servicio.ActualizarAsync(id, request, cancellationToken)).ToActionResult();
}
