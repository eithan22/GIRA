using GIRA.Api.Common;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Reservas;

/// <summary>Administración de zonas (GI-193, GI-196). Solo Gerente y Administrador.</summary>
[ApiController]
[Route("api/zonas")]
[Authorize(Policy = Politicas.GestionarSalon)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class ZonasController : ControllerBase
{
    private readonly IZonaService _zonas;

    /// <summary>Crea el controlador con el servicio de zonas.</summary>
    public ZonasController(IZonaService zonas)
    {
        _zonas = zonas;
    }

    /// <summary>Lista paginada (page/pageSize, máximo 100), filtrable por estado.</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ZonaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Listar([FromQuery] ListarSalonRequest request, CancellationToken ct)
        => (await _zonas.ListarAsync(request, ct)).ToActionResult();

    /// <summary>Obtiene una zona por su id. 404 si no existe.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<ZonaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken ct)
        => (await _zonas.ObtenerAsync(id, ct)).ToActionResult();

    /// <summary>Crea una zona. 409 si el nombre ya existe.</summary>
    [HttpPost]
    [ProducesResponseType<ZonaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear([FromBody] GuardarZonaRequest request, CancellationToken ct)
        => (await _zonas.CrearAsync(request, ct)).ToActionResult();

    /// <summary>Actualiza una zona. 404 si no existe; 409 si el nombre ya existe o hay conflicto de concurrencia.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<ZonaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] GuardarZonaRequest request, CancellationToken ct)
        => (await _zonas.ActualizarAsync(id, request, ct)).ToActionResult();

    /// <summary>Activa o desactiva. 409 si sus mesas tienen reservas futuras vigentes.</summary>
    [HttpPatch("{id:guid}/estado")]
    [ProducesResponseType<ZonaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] CambiarEstadoSalonRequest request, CancellationToken ct)
        => (await _zonas.CambiarEstadoAsync(id, request.Activa, ct)).ToActionResult();
}
