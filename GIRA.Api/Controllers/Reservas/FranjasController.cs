using GIRA.Api.Common;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Reservas;

/// <summary>Administración de franjas horarias (GI-195, GI-196). Solo Gerente y Administrador.</summary>
[ApiController]
[Route("api/franjas")]
[Authorize(Policy = Politicas.GestionarSalon)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class FranjasController : ControllerBase
{
    private readonly IFranjaService _franjas;

    /// <summary>Crea el controlador con el servicio de franjas.</summary>
    public FranjasController(IFranjaService franjas)
    {
        _franjas = franjas;
    }

    /// <summary>Lista paginada (page/pageSize, máximo 100), filtrable por estado.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<FranjaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Listar([FromQuery] ListarSalonRequest request, CancellationToken ct)
        => (await _franjas.ListarAsync(request, ct)).ToActionResult();

    /// <summary>Obtiene una franja por su id. 404 si no existe.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<FranjaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken ct)
        => (await _franjas.ObtenerAsync(id, ct)).ToActionResult();

    /// <summary>Crea una franja. 400 si el horario es inválido; 409 si el nombre ya existe o se solapa con otra franja activa.</summary>
    [HttpPost]
    [ProducesResponseType<FranjaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear([FromBody] GuardarFranjaRequest request, CancellationToken ct)
        => (await _franjas.CrearAsync(request, ct)).ToActionResult();

    /// <summary>Actualiza una franja. 404 si no existe; 409 si hay duplicado, solapamiento, reservas futuras o conflicto de concurrencia.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<FranjaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] GuardarFranjaRequest request, CancellationToken ct)
        => (await _franjas.ActualizarAsync(id, request, ct)).ToActionResult();

    /// <summary>Activa o desactiva. 409 si la franja tiene reservas futuras vigentes.</summary>
    [HttpPatch("{id:guid}/estado")]
    [ProducesResponseType<FranjaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] CambiarEstadoSalonRequest request, CancellationToken ct)
        => (await _franjas.CambiarEstadoAsync(id, request.Activa, ct)).ToActionResult();
}
