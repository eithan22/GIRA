using GIRA.Api.Common;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Inventario;
using GIRA.Application.Interfaces.Services.Inventario;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Inventario;

/// <summary>Proyección de consumo (RF-INV-07, RF-INV-12) y panel de alertas (RF-INV-09, RF-INV-10). Supervisor, Gerente y Administrador.</summary>
[ApiController]
[Route("api/inventario")]
[Authorize(Policy = Politicas.AtenderAlertasInventario)]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class InventarioController : ControllerBase
{
    private readonly IProyeccionConsumoService _proyeccion;
    private readonly IAlertaInventarioService _alertas;

    /// <summary>Crea el controlador.</summary>
    public InventarioController(IProyeccionConsumoService proyeccion, IAlertaInventarioService alertas)
    {
        _proyeccion = proyeccion;
        _alertas = alertas;
    }

    /// <summary>Recalcula y devuelve el consumo proyectado por insumo y día (por defecto, 7 días desde hoy; máximo 14).</summary>
    [HttpGet("consumo-proyectado")]
    [ProducesResponseType<ProyeccionConsumoDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ConsumoProyectado([FromQuery] ProyeccionRequest request, CancellationToken ct)
        => (await _proyeccion.ObtenerAsync(request, ct)).ToActionResult();

    /// <summary>Alertas por estado (Abierta, Atendida, Descartada), las más nuevas primero.</summary>
    [HttpGet("alertas")]
    [ProducesResponseType<PagedResult<AlertaInventarioDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Alertas([FromQuery] ListarAlertasRequest request, CancellationToken ct)
        => (await _alertas.ListarAsync(request, ct)).ToActionResult();

    /// <summary>Marca una alerta como Atendida o Descartada. 404 si no existe; 409 si ya no está abierta.</summary>
    [HttpPatch("alertas/{id:guid}")]
    [ProducesResponseType<AlertaInventarioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> CambiarEstadoAlerta(Guid id, [FromBody] CambiarEstadoAlertaRequest request, CancellationToken ct)
        => (await _alertas.CambiarEstadoAsync(id, request, User.ObtenerUsuarioId(), ct)).ToActionResult();
}
