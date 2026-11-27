using GIRA.Api.Common;
using GIRA.Application.DTOs.Configuracion;
using GIRA.Application.Interfaces.Services.Configuracion;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Configuracion;

/// <summary>Bandeja de notificaciones del usuario autenticado (SRS 5.6, RF-CFG-05). Cada quien ve solo las suyas.</summary>
[ApiController]
[Authorize]
[Route("api/notificaciones")]
[ProducesResponseType(StatusCodes.Status401Unauthorized)]
public class NotificacionesController : ControllerBase
{
    private readonly INotificacionService _servicio;

    /// <summary>Crea el controlador.</summary>
    public NotificacionesController(INotificacionService servicio)
    {
        _servicio = servicio;
    }

    /// <summary>Las 50 más recientes y el total de no leídas. <paramref name="leidas"/> filtra (true o false); sin él, todas.</summary>
    [HttpGet("mias")]
    [ProducesResponseType<BandejaNotificacionesDto>(StatusCodes.Status200OK)]
    public async Task<IActionResult> Mias([FromQuery] bool? leidas, CancellationToken ct)
    {
        if (User.ObtenerUsuarioId() is not { } usuarioId)
        {
            return Unauthorized();
        }
        return Ok(await _servicio.ListarMiasAsync(usuarioId, leidas, ct));
    }

    /// <summary>Marca como leída una notificación propia. 404 si no existe o es de otro usuario.</summary>
    [HttpPatch("{id:guid}/leida")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> MarcarLeida(Guid id, CancellationToken ct)
    {
        if (User.ObtenerUsuarioId() is not { } usuarioId)
        {
            return Unauthorized();
        }
        return (await _servicio.MarcarLeidaAsync(id, usuarioId, ct)).ToActionResult();
    }
}
