using GIRA.Api.Common;
using GIRA.Application.DTOs.Inventario;
using GIRA.Application.Interfaces.Services.Inventario;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Inventario;

/// <summary>
/// Catálogos de Inventario (RF-INV-02, RF-INV-13): unidades de medida, categorías de insumo y de plato, y proveedores.
/// Las unidades y categorías las lee quien consulta inventario; todo lo demás es de Gerente y Administrador.
/// La política va en cada método: si también hubiera una en la clase, habría que cumplir las dos.
/// </summary>
[ApiController]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class CatalogosInventarioController : ControllerBase
{
    private readonly ICatalogoInventarioService _catalogos;

    /// <summary>Crea el controlador.</summary>
    public CatalogosInventarioController(ICatalogoInventarioService catalogos)
    {
        _catalogos = catalogos;
    }

    /// <summary>Unidades de medida por nombre.</summary>
    [HttpGet("api/unidades-medida")]
    [Authorize(Policy = Politicas.ConsultarInventario)]
    [ProducesResponseType<IReadOnlyList<UnidadMedidaDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarUnidades(CancellationToken ct)
        => (await _catalogos.ListarUnidadesAsync(ct)).ToActionResult();

    /// <summary>Crea una unidad. 409 si el nombre o la abreviatura ya existen.</summary>
    [HttpPost("api/unidades-medida")]
    [Authorize(Policy = Politicas.GestionarInventario)]
    [ProducesResponseType<UnidadMedidaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearUnidad([FromBody] GuardarUnidadMedidaRequest request, CancellationToken ct)
        => (await _catalogos.CrearUnidadAsync(request, ct)).ToActionResult();

    /// <summary>Edita una unidad. 404 si no existe; 409 si el nombre o la abreviatura ya existen.</summary>
    [HttpPut("api/unidades-medida/{id:guid}")]
    [Authorize(Policy = Politicas.GestionarInventario)]
    [ProducesResponseType<UnidadMedidaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarUnidad(Guid id, [FromBody] GuardarUnidadMedidaRequest request, CancellationToken ct)
        => (await _catalogos.ActualizarUnidadAsync(id, request, ct)).ToActionResult();

    /// <summary>Categorías de insumo por nombre.</summary>
    [HttpGet("api/categorias-insumo")]
    [Authorize(Policy = Politicas.ConsultarInventario)]
    [ProducesResponseType<IReadOnlyList<CategoriaDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarCategoriasInsumo(CancellationToken ct)
        => (await _catalogos.ListarCategoriasInsumoAsync(ct)).ToActionResult();

    /// <summary>Crea una categoría de insumo. 409 si el nombre ya existe.</summary>
    [HttpPost("api/categorias-insumo")]
    [Authorize(Policy = Politicas.GestionarInventario)]
    [ProducesResponseType<CategoriaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearCategoriaInsumo([FromBody] GuardarCategoriaRequest request, CancellationToken ct)
        => (await _catalogos.CrearCategoriaInsumoAsync(request, ct)).ToActionResult();

    /// <summary>Edita una categoría de insumo. 404 si no existe; 409 si el nombre ya existe.</summary>
    [HttpPut("api/categorias-insumo/{id:guid}")]
    [Authorize(Policy = Politicas.GestionarInventario)]
    [ProducesResponseType<CategoriaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarCategoriaInsumo(Guid id, [FromBody] GuardarCategoriaRequest request, CancellationToken ct)
        => (await _catalogos.ActualizarCategoriaInsumoAsync(id, request, ct)).ToActionResult();

    /// <summary>Categorías de plato por nombre.</summary>
    [HttpGet("api/categorias-plato")]
    [Authorize(Policy = Politicas.ConsultarInventario)]
    [ProducesResponseType<IReadOnlyList<CategoriaDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarCategoriasPlato(CancellationToken ct)
        => (await _catalogos.ListarCategoriasPlatoAsync(ct)).ToActionResult();

    /// <summary>Crea una categoría de plato. 409 si el nombre ya existe.</summary>
    [HttpPost("api/categorias-plato")]
    [Authorize(Policy = Politicas.GestionarInventario)]
    [ProducesResponseType<CategoriaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearCategoriaPlato([FromBody] GuardarCategoriaRequest request, CancellationToken ct)
        => (await _catalogos.CrearCategoriaPlatoAsync(request, ct)).ToActionResult();

    /// <summary>Edita una categoría de plato. 404 si no existe; 409 si el nombre ya existe.</summary>
    [HttpPut("api/categorias-plato/{id:guid}")]
    [Authorize(Policy = Politicas.GestionarInventario)]
    [ProducesResponseType<CategoriaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarCategoriaPlato(Guid id, [FromBody] GuardarCategoriaRequest request, CancellationToken ct)
        => (await _catalogos.ActualizarCategoriaPlatoAsync(id, request, ct)).ToActionResult();

    /// <summary>Proveedores por nombre.</summary>
    [HttpGet("api/proveedores")]
    [Authorize(Policy = Politicas.GestionarInventario)]
    [ProducesResponseType<IReadOnlyList<ProveedorDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> ListarProveedores(CancellationToken ct)
        => (await _catalogos.ListarProveedoresAsync(ct)).ToActionResult();

    /// <summary>Crea un proveedor. 409 si el nombre ya existe.</summary>
    [HttpPost("api/proveedores")]
    [Authorize(Policy = Politicas.GestionarInventario)]
    [ProducesResponseType<ProveedorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CrearProveedor([FromBody] GuardarProveedorRequest request, CancellationToken ct)
        => (await _catalogos.CrearProveedorAsync(request, ct)).ToActionResult();

    /// <summary>Edita un proveedor. 404 si no existe; 409 si el nombre ya existe.</summary>
    [HttpPut("api/proveedores/{id:guid}")]
    [Authorize(Policy = Politicas.GestionarInventario)]
    [ProducesResponseType<ProveedorDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ActualizarProveedor(Guid id, [FromBody] GuardarProveedorRequest request, CancellationToken ct)
        => (await _catalogos.ActualizarProveedorAsync(id, request, ct)).ToActionResult();
}
