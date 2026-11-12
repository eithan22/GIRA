using GIRA.Api.Common;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Reservas;

/// <summary>Búsqueda de clientes para el personal (GI-164, GI-198, GI-199). Política OperarReservas.</summary>
[ApiController]
[Route("api/clientes")]
[Authorize(Policy = Politicas.OperarReservas)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
[ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden)]
public class ClientesController : ControllerBase
{
    private readonly IClienteService _clientes;

    /// <summary>Crea el controlador con el servicio de clientes.</summary>
    public ClientesController(IClienteService clientes)
    {
        _clientes = clientes;
    }

    /// <summary>
    /// Busca clientes por parte del nombre o del teléfono (mínimo 3 caracteres), paginado
    /// (page/pageSize, máximo 100), para ayudar a quien perdió su código.
    /// </summary>
    [HttpGet]
    [ProducesResponseType<PagedResult<ClienteDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Buscar([FromQuery] BuscarClientesRequest request, CancellationToken ct)
        => (await _clientes.BuscarAsync(request, ct)).ToActionResult();
}
