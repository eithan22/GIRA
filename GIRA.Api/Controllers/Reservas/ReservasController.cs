using GIRA.Api.Common;
using GIRA.Api.Common.Reservas;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Services.Reservas;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GIRA.Api.Controllers.Reservas;

/// <summary>Endpoints públicos y del personal del módulo Reservas.</summary>
[ApiController]
[Route("api/reservas")]
public class ReservasController : ControllerBase
{
    private readonly IDisponibilidadService _disponibilidadService;

    /// <summary>Crea el controller con el servicio de disponibilidad.</summary>
    public ReservasController(IDisponibilidadService disponibilidadService)
    {
        _disponibilidadService = disponibilidadService;
    }

    /// <summary>
    /// GI-157: franjas con al menos una mesa disponible para una fecha y cantidad de
    /// personas. Endpoint público, sin información de cuántas mesas quedan.
    /// </summary>
    [HttpGet("disponibilidad")]
    [AllowAnonymous]
    [EnableRateLimiting(LimiteReservasExtensions.PoliticaConsulta)]
    [ProducesResponseType<IReadOnlyList<FranjaDisponibleDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> ConsultarDisponibilidad( [FromQuery] DateOnly fecha, [FromQuery] int personas, CancellationToken cancellationToken)
    {
        var resultado = await _disponibilidadService.ConsultarAsync(
            new ConsultarDisponibilidadRequest(fecha, personas), cancellationToken);

        return resultado.ToActionResult();
    }
}
