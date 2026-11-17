using GIRA.Api.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Reservas;

/// <summary>Franjas para el personal operativo (GI-399). Política OperarReservas.</summary>
/// <remarks>
/// Vive en su propio controlador con la ruta literal <c>api/reservas/franjas</c>: no choca con
/// <c>GET api/reservas/{id:guid}</c> porque "franjas" no es un GUID, ni con <c>disponibilidad</c> ni
/// <c>ocupacion</c>, que son otros literales. Es aparte de <c>api/franjas</c> (GestionarSalon, solo
/// Administrador y Gerente), que no se toca.
/// </remarks>
[ApiController]
[Route("api/reservas/franjas")]
[Authorize(Policy = Politicas.OperarReservas)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public class FranjasOperativasController : ControllerBase
{
    private readonly IFranjaService _franjas;

    /// <summary>Crea el controlador con el servicio de franjas.</summary>
    public FranjasOperativasController(IFranjaService franjas)
    {
        _franjas = franjas;
    }

    /// <summary>
    /// Todas las franjas no borradas, activas e inactivas (con su bandera <c>activa</c>), ordenadas por
    /// hora de inicio y sin paginar. Es la fuente de franjas de Mesero y Supervisor para los filtros,
    /// el mapa y las visitas.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<FranjaOperativaDto>>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Listar(CancellationToken cancellationToken)
        => (await _franjas.ListarOperativasAsync(cancellationToken)).ToActionResult();
}
