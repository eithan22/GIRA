using System.Security.Claims;
using GIRA.Api.Common;
using GIRA.Application.Common;
using GIRA.Application.Interfaces.Services.Seguridad;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Controllers.Seguridad;

/// <summary>Datos del usuario autenticado (GI-147).</summary>
[ApiController]
[Route("api/auth/me")]
[Authorize]
public class MeController : ControllerBase
{
    private readonly IUsuarioService _usuarios;

    public MeController(IUsuarioService usuarios)
    {
        _usuarios = usuarios;
    }

    [HttpGet]
    public async Task<IActionResult> Obtener(CancellationToken ct)
    {
        var claim = User.FindFirstValue(ClaimTypes.NameIdentifier)
                    ?? User.FindFirstValue("sub");

        if (!Guid.TryParse(claim, out var usuarioId))
            return OperationResult.Failure<object>(ErrorType.Unauthorized, "Token inválido.")
                .ToActionResult();

        return (await _usuarios.ObtenerActualAsync(usuarioId, ct)).ToActionResult();
    }
}