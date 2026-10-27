using GIRA.Api.Common;
using GIRA.Api.Common.Seguridad;
using GIRA.Application.DTOs.Seguridad.Auth;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace GIRA.Api.Controllers.Seguridad;

/// <summary>Autenticación del personal (RF-SEG-01).</summary>
[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    private readonly IAuthService _authService;

    /// <summary>Crea el controlador con el servicio de autenticación.</summary>
    public AuthController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>Inicia sesión con correo y contraseña; devuelve los tokens y los datos del usuario.</summary>
    [HttpPost("login")]
    [AllowAnonymous]
    [EnableRateLimiting(LimiteLoginExtensions.Politica)]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Login(LoginRequest request, CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida";
        var resultado = await _authService.LoginAsync(request, ip, cancellationToken);
        return resultado.ToActionResult();
    }
    /// <summary>
    /// GI-120: Renueva la sesión mediante un refresh token.
    /// No requiere un access token válido.
    /// </summary>
    [HttpPost("refresh")]
    [AllowAnonymous]
    [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh(
        RefreshTokenRequest request,
        CancellationToken cancellationToken)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?
            .ToString() ?? "desconocida";

        var resultado = await _authService.RefreshAsync(
            request.RefreshToken,
            ip,
            cancellationToken);

        return resultado.ToActionResult();
    }

    /// <summary>
    /// GI-120: Cierra la sesión revocando el refresh token.
    /// Requiere un usuario autenticado.
    /// </summary>
    [HttpPost("logout")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Logout(
        LogoutRequest request,
        CancellationToken cancellationToken)
    {
        var usuarioId = User.ObtenerUsuarioId();

        if (usuarioId is null)
        {
            return Unauthorized();
        }

        var resultado = await _authService.LogoutAsync(
            usuarioId.Value,
            request.RefreshToken,
            cancellationToken);

        return resultado.ToActionResult();
    }


    /// <summary>
    /// GI-143: Cambia la contraseña del usuario autenticado.
    /// </summary>
    [HttpPut("cambiar-password")]
    [Authorize]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> CambiarPassword(
        [FromBody] CambiarPasswordRequest request,
        CancellationToken cancellationToken)
    {
        var usuarioId = User.ObtenerUsuarioId();

        if (usuarioId is null)
            return Unauthorized();

        var resultado = await _authService.CambiarPasswordAsync(
            usuarioId.Value,
            request,
            cancellationToken);

        if (!resultado.IsSuccess)
            return resultado.ToActionResult();

        return NoContent();
    }

}
