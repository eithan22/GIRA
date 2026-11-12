using GIRA.Api.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Reservas;

/// <summary>Mapa de ocupación de mesas del personal (GI-167, GI-205, GI-206). Política OperarReservas.</summary>
/// <remarks>
/// Vive en su propio controlador con la ruta literal <c>api/reservas/ocupacion</c>: no choca con
/// <c>GET api/reservas/{id:guid}</c> porque "ocupacion" no es un GUID.
/// </remarks>
[ApiController]
[Route("api/reservas/ocupacion")]
[Authorize(Policy = Politicas.OperarReservas)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public class OcupacionController : ControllerBase
{
    private readonly IOcupacionService _ocupacion;

    /// <summary>Crea el controlador con el servicio de ocupación.</summary>
    public OcupacionController(IOcupacionService ocupacion)
    {
        _ocupacion = ocupacion;
    }

    /// <summary>
    /// Estado de cada mesa (Libre, Reservada, Ocupada o Inactiva) por zona para una fecha y una franja. La fecha es
    /// opcional (por defecto hoy, formato aaaa-mm-dd) y la franja obligatoria. Las visitas sin reserva no cuentan.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<MapaOcupacionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> ObtenerMapa([FromQuery] ConsultarOcupacionRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _ocupacion.ObtenerMapaAsync(request, cancellationToken);

        return resultado.ToActionResult();
    }
}
