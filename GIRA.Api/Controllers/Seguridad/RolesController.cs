using GIRA.Api.Common;
using GIRA.Application.Interfaces.Services.Seguridad.Roles;
using GIRA.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Seguridad;

/// <summary>Catálogo de roles del sistema (GI-99). Solo Administrador.</summary>
[ApiController]
[Route("api/roles")]
[Authorize(Policy = Politicas.GestionarUsuarios)]
public class RolesController : ControllerBase
{
    private readonly IRolService _roles;

    /// <summary>Crea el controlador con el servicio de roles.</summary>
    public RolesController(IRolService roles)
    {
        _roles = roles;
    }

    /// <summary>Lista los roles existentes para el selector de roles.</summary>
    [HttpGet]
    public async Task<IActionResult> Listar(CancellationToken ct)
        => (await _roles.ListarAsync(ct)).ToActionResult();
}
