using GIRA.Api.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Reservas;

/// <summary>Visitas sin reserva (walk-ins) del personal (GI-166, GI-203, GI-204). Política OperarReservas.</summary>
[ApiController]
[Route("api/visitas")]
[Authorize(Policy = Politicas.OperarReservas)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public class VisitasController : ControllerBase
{
    private readonly IVisitaService _visitas;

    /// <summary>Crea el controlador con el servicio de visitas.</summary>
    public VisitasController(IVisitaService visitas)
    {
        _visitas = visitas;
    }

    /// <summary>
    /// Registra una visita sin reserva de hoy (la fecha la pone el servidor). La franja debe estar
    /// activa y haber empezado; la mesa, si se envía, debe estar activa y libre en esa franja.
    /// Publica <c>VisitaCompletadaEvent</c> con <c>ConReserva = false</c>. Devuelve 200, como el
    /// resto del proyecto.
    /// </summary>
    [HttpPost]
    [ProducesResponseType<VisitaDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Registrar(RegistrarVisitaRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _visitas.RegistrarAsync(request, cancellationToken);

        return resultado.ToActionResult();
    }
}
