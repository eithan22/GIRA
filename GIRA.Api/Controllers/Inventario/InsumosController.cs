using GIRA.Api.Common;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Inventario;
using GIRA.Application.Interfaces.Services.Inventario;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Inventario;

/// <summary>Catálogo de insumos (RF-INV-01). Lo consultan Cocina, Supervisor, Gerente y Administrador; lo editan Gerente y Administrador.</summary>
[ApiController]
[Route("api/insumos")]
[Authorize(Policy = Politicas.ConsultarInventario)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class InsumosController : ControllerBase
{
    private readonly IInsumoService _insumos;

    /// <summary>Crea el controlador.</summary>
    public InsumosController(IInsumoService insumos)
    {
        _insumos = insumos;
    }

    /// <summary>Lista paginada con stock y nivel; filtra por texto, categoría y nivel (Normal, Bajo, Critico).</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<InsumoDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Listar([FromQuery] ListarInsumosRequest request, CancellationToken ct)
        => (await _insumos.ListarAsync(request, ct)).ToActionResult();

    /// <summary>Cuántos insumos activos hay y cuántos están en nivel bajo o crítico.</summary>
    [HttpGet("resumen")]
    [ProducesResponseType<ResumenInsumosDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Resumen(CancellationToken ct)
        => (await _insumos.ResumenAsync(ct)).ToActionResult();

    /// <summary>Un insumo por id. 404 si no existe.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<InsumoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken ct)
        => (await _insumos.ObtenerAsync(id, ct)).ToActionResult();

    /// <summary>Crea un insumo con stock 0 (el stock entra con movimientos). 409 si el nombre ya existe.</summary>
    [HttpPost]
    [Authorize(Policy = Politicas.GestionarInventario)]
    [ProducesResponseType<InsumoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear([FromBody] GuardarInsumoRequest request, CancellationToken ct)
        => (await _insumos.CrearAsync(request, ct)).ToActionResult();

    /// <summary>Edita un insumo (no su stock). 404 si no existe; 409 si el nombre ya existe.</summary>
    [HttpPut("{id:guid}")]
    [Authorize(Policy = Politicas.GestionarInventario)]
    [ProducesResponseType<InsumoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] GuardarInsumoRequest request, CancellationToken ct)
        => (await _insumos.ActualizarAsync(id, request, ct)).ToActionResult();

    /// <summary>Activa o desactiva. 409 si se quiere desactivar y lo usa una receta activa (RN-INV-10).</summary>
    [HttpPatch("{id:guid}/estado")]
    [Authorize(Policy = Politicas.GestionarInventario)]
    [ProducesResponseType<InsumoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] CambiarEstadoInsumoRequest request, CancellationToken ct)
        => (await _insumos.CambiarEstadoAsync(id, request.Activo, ct)).ToActionResult();
}
