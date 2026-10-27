using GIRA.Application.DTOs.Seguridad.Auth;

namespace GIRA.Application.Interfaces.Services.Seguridad.Auth;

/// <summary>
/// Emite los tokens de sesión. Recibe datos simples, no la entidad Usuario, para que
/// el login (GI-95) y la renovación (GI-96) lo reutilicen igual.
/// </summary>
public interface ITokenService
{
    /// <summary>Genera el access token JWT con los roles del usuario.</summary>
    TokenGenerado GenerarAccessToken(Guid usuarioId, string email, string nombreCompleto, IEnumerable<string> roles);

    /// <summary>Genera un refresh token, guarda solo su hash y devuelve el valor en claro para el cliente.</summary>
    Task<TokenGenerado> CrearRefreshTokenAsync(Guid usuarioId, string ip, CancellationToken cancellationToken = default);

    /// <summary>Hash SHA-256 en hexadecimal con el que se guarda y se busca un refresh token.</summary>
    string CalcularHash(string token);
}
