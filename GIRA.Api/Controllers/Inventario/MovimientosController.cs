using GIRA.Api.Common;
using GIRA.Application.DTOs.Inventario;
using GIRA.Application.Interfaces.Services.Inventario;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Inventario;

/// <summary>Movimientos de inventario (RF-INV-05), kardex (RF-INV-06) y conteo físico (RF-INV-11).</summary>
[ApiController]
[Route("api/movimientos")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class MovimientosController : ControllerBase
{
    private readonly IMovimientoInventarioService _movimientos;

    /// <summary>Crea el controlador.</summary>
    public MovimientosController(IMovimientoInventarioService movimientos)
    {
        _movimientos = movimientos;
    }

    /// <summary>
    /// Registra una entrada, merma o ajuste. 400 si faltan datos o si el stock quedaría negativo (RN-INV-02);
    /// 404 si el insumo no existe; 409 si está inactivo o si otro movimiento lo cambió al mismo tiempo.
    /// </summary>
    [HttpPost]
    [Authorize(Policy = Politicas.RegistrarMovimientos)]
    [ProducesResponseType<MovimientoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Registrar([FromBody] RegistrarMovimientoRequest request, CancellationToken ct)
        => (await _movimientos.RegistrarAsync(request, User.ObtenerUsuarioId(), ct)).ToActionResult();

    /// <summary>Kardex de un insumo entre dos fechas (por defecto, los últimos 30 días) con el saldo después de cada movimiento.</summary>
    [HttpGet]
    [Authorize(Policy = Politicas.AtenderAlertasInventario)]
    [ProducesResponseType<KardexDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Kardex([FromQuery] KardexRequest request, CancellationToken ct)
        => (await _movimientos.KardexAsync(request, ct)).ToActionResult();

    /// <summary>Conteo físico: genera un ajuste por cada diferencia contra el stock, todo junto.</summary>
    [HttpPost("conteo-fisico")]
    [Authorize(Policy = Politicas.AtenderAlertasInventario)]
    [ProducesResponseType<ConteoFisicoResultadoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConteoFisico([FromBody] ConteoFisicoRequest request, CancellationToken ct)
        => (await _movimientos.ConteoFisicoAsync(request, User.ObtenerUsuarioId(), ct)).ToActionResult();
}
