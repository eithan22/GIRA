using GIRA.Api.Common;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Reservas;

/// <summary>Administración de mesas (GI-194, GI-196). Solo Gerente y Administrador.</summary>
[ApiController]
[Route("api/mesas")]
[Authorize(Policy = Politicas.GestionarSalon)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class MesasController : ControllerBase
{
    private readonly IMesaService _mesas;

    /// <summary>Crea el controlador con el servicio de mesas.</summary>
    public MesasController(IMesaService mesas)
    {
        _mesas = mesas;
    }

    /// <summary>Lista paginada, filtrable por zona (zonaId) y estado (activa).</summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<MesaDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Listar([FromQuery] ListarMesasRequest request, CancellationToken ct)
        => (await _mesas.ListarAsync(request, ct)).ToActionResult();

    /// <summary>Obtiene una mesa por su id. 404 si no existe.</summary>
    [HttpGet("{id:guid}")]
    [ProducesResponseType<MesaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken ct)
        => (await _mesas.ObtenerAsync(id, ct)).ToActionResult();

    /// <summary>Crea una mesa. 409 si el número ya existe o la zona está inactiva.</summary>
    [HttpPost]
    [ProducesResponseType<MesaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Crear([FromBody] GuardarMesaRequest request, CancellationToken ct)
        => (await _mesas.CrearAsync(request, ct)).ToActionResult();

    /// <summary>Actualiza una mesa. 404 si no existe; 409 si hay duplicado, reservas futuras que lo impidan o conflicto de concurrencia.</summary>
    [HttpPut("{id:guid}")]
    [ProducesResponseType<MesaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] GuardarMesaRequest request, CancellationToken ct)
        => (await _mesas.ActualizarAsync(id, request, ct)).ToActionResult();

    /// <summary>Activa o desactiva. 409 si la mesa tiene reservas futuras vigentes (RN-RES-12).</summary>
    [HttpPatch("{id:guid}/estado")]
    [ProducesResponseType<MesaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] CambiarEstadoSalonRequest request, CancellationToken ct)
        => (await _mesas.CambiarEstadoAsync(id, request.Activa, ct)).ToActionResult();
}
