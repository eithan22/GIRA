using GIRA.Api.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Reservas;

// <summary>Administración de zonas(GI - 193, GI - 196).Solo Gerente y Administrador.</summary>
[ApiController]
[Route("api/zonas")]
[Authorize(Policy = Politicas.GestionarSalon)]
public class ZonasController : ControllerBase
{
    private readonly IZonaService _zonas;

    public ZonasController(IZonaService zonas)
    {
        _zonas = zonas;
    }

    /// <summary>Lista paginada (page/pageSize, máximo 100), filtrable por estado.</summary>
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] ListarSalonRequest request, CancellationToken ct)
        => (await _zonas.ListarAsync(request, ct)).ToActionResult();

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken ct)
        => (await _zonas.ObtenerAsync(id, ct)).ToActionResult();

    /// <summary>Crea una zona. 409 si el nombre ya existe.</summary>
    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] GuardarZonaRequest request, CancellationToken ct)
        => (await _zonas.CrearAsync(request, ct)).ToActionResult();

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] GuardarZonaRequest request, CancellationToken ct)
        => (await _zonas.ActualizarAsync(id, request, ct)).ToActionResult();

    /// <summary>Activa o desactiva. 409 si sus mesas tienen reservas futuras vigentes.</summary>
    [HttpPatch("{id:guid}/estado")]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] CambiarEstadoSalonRequest request, CancellationToken ct)
        => (await _zonas.CambiarEstadoAsync(id, request.Activa, ct)).ToActionResult();
}