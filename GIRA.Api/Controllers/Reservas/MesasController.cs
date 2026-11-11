using GIRA.Api.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Reservas;


[ApiController]
[Route("api/mesas")]
[Authorize(Policy = Politicas.GestionarSalon)]
public class MesasController : ControllerBase
{
    private readonly IMesaService _mesas;

    public MesasController(IMesaService mesas)
    {
        _mesas = mesas;
    }

    /// <summary>Lista paginada, filtrable por zona (zonaId) y estado (activa).</summary>
    [HttpGet]
    public async Task<IActionResult> Listar([FromQuery] ListarMesasRequest request, CancellationToken ct)
        => (await _mesas.ListarAsync(request, ct)).ToActionResult();

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> Obtener(Guid id, CancellationToken ct)
        => (await _mesas.ObtenerAsync(id, ct)).ToActionResult();

    [HttpPost]
    public async Task<IActionResult> Crear([FromBody] GuardarMesaRequest request, CancellationToken ct)
        => (await _mesas.CrearAsync(request, ct)).ToActionResult();

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Actualizar(Guid id, [FromBody] GuardarMesaRequest request, CancellationToken ct)
        => (await _mesas.ActualizarAsync(id, request, ct)).ToActionResult();

    [HttpPatch("{id:guid}/estado")]
    public async Task<IActionResult> CambiarEstado(Guid id, [FromBody] CambiarEstadoSalonRequest request, CancellationToken ct)
        => (await _mesas.CambiarEstadoAsync(id, request.Activa, ct)).ToActionResult();
}
