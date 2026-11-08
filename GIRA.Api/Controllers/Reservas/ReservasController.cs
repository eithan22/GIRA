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
    private readonly IReservaService _reservaService;

    /// <summary>Crea el controller con los servicios de disponibilidad y reservas.</summary>
    public ReservasController(IDisponibilidadService disponibilidadService, IReservaService reservaService)
    {
        _disponibilidadService = disponibilidadService;
        _reservaService = reservaService;
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

    /// <summary>
    /// GI-158: crea una reserva como invitado (sin cuenta), con la mesa asignada
    /// automáticamente. Endpoint público; devuelve 200 al crear, como el resto del proyecto.
    /// </summary>
    /// <remarks>
    /// Esta historia es la ruta "sin token": siempre fija Origen = Web y no lee el token de
    /// la solicitud. GI-222 agregará la lectura del token para Origen = Telefono/Presencial
    /// y el usuario que registra, cuando el personal cree la reserva desde este mismo endpoint.
    /// </remarks>
    [HttpPost]
    [AllowAnonymous]
    [EnableRateLimiting(LimiteReservasExtensions.PoliticaCreacion)]
    [ProducesResponseType<ReservaCreadaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Crear(CrearReservaRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _reservaService.CrearAsync(request, cancellationToken);

        return resultado.ToActionResult();
    }
}
