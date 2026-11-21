using GIRA.Api.Common;
using GIRA.Application.DTOs.Configuracion;
using GIRA.Application.Interfaces.Services.Configuracion;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Configuracion;

/// <summary>
/// Parámetros del restaurante que usa Inventario (SRS 5.6, GI-468): días de proyección para avisar un quiebre y
/// días de aviso antes de un vencimiento. Los lee quien atiende alertas; los cambian Administrador y Gerente.
/// </summary>
[ApiController]
[Route("api/configuracion/inventario")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
[ProducesResponseType(StatusCodes.Status403Forbidden)]
public class ConfiguracionInventarioController : ControllerBase
{
    private readonly IParametrosInventarioService _servicio;

    /// <summary>Crea el controlador.</summary>
    public ConfiguracionInventarioController(IParametrosInventarioService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>Valores vigentes (3 y 2 días si nunca se guardaron).</summary>
    [HttpGet]
    [Authorize(Policy = Politicas.AtenderAlertasInventario)]
    [ProducesResponseType<ParametrosInventarioDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Obtener(CancellationToken ct) => Ok(await _servicio.ObtenerAsync(ct));

    /// <summary>Guarda los dos valores. 400 si salen de rango; 409 si otra persona guardó al mismo tiempo.</summary>
    [HttpPut]
    [Authorize(Policy = Politicas.GestionarConfiguracion)]
    [ProducesResponseType<ParametrosInventarioDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Actualizar([FromBody] ActualizarParametrosInventarioRequest request, CancellationToken ct)
        => (await _servicio.ActualizarAsync(request, ct)).ToActionResult();
}
