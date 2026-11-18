using GIRA.Api.Common;
using GIRA.Application.DTOs.Inventario;
using GIRA.Application.Interfaces.Services.Inventario;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Inventario;

/// <summary>Platos y recetas (RF-INV-03, RF-INV-04). Cocina puede leerlos; los editan Gerente y Administrador.</summary>
[ApiController]
[Route("api/platos")]
[Authorize(Policy = Politicas.ConsultarInventario)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class PlatosController : ControllerBase
{
    private readonly IPlatoService _platos;

    /// <summary>Crea el controlador.</summary>
    public PlatosController(IPlatoService platos)
    {
        _platos = platos;
    }

    /// <summary>Todos los platos y la suma de demanda de los activos, con aviso si no está entre 95 % y 105 % (RN-INV-05).</summary>
    [HttpGet]
    [ProducesResponseType<ListadoPlatosDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => (await _platos.ListarAsync(ct)).ToActionResult();

    /// <summary>Crea un plato. 400 si la categoría no existe; 409 si el nombre ya existe.</summary>
    [HttpPost]
    [Authorize(Policy = Politicas.GestionarInventario)]
    [ProducesResponseType<PlatoGuardadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear([FromBody] GuardarPlatoRequest request, CancellationToken ct)
        => (await _platos.CrearAsync(request, ct)).ToActionResult();

    /// <summary>Edita un plato. 404 si no existe; 409 si el nombre ya existe.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Politicas.GestionarInventario)]
    [ProducesResponseType<PlatoGuardadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] GuardarPlatoRequest request, CancellationToken ct)
        => (await _platos.ActualizarAsync(id, request, ct)).ToActionResult();

    /// <summary>Receta del plato (Porciones vacío si todavía no tiene). 404 si el plato no existe.</summary>
    [HttpGet("{id:guid}/receta")]
    [ProducesResponseType<RecetaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerReceta(Guid id, CancellationToken ct)
        => (await _platos.ObtenerRecetaAsync(id, ct)).ToActionResult();

    /// <summary>Reemplaza la receta. 404 si el plato no existe; 400 si una línea no sirve.</summary>
    [HttpPut("{id:guid}/receta")]
    [Authorize(Policy = Politicas.GestionarInventario)]
    [ProducesResponseType<RecetaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> GuardarReceta(Guid id, [FromBody] GuardarRecetaRequest request, CancellationToken ct)
        => (await _platos.GuardarRecetaAsync(id, request, ct)).ToActionResult();
}
