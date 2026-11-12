using GIRA.Application.Common;
using GIRA.Application.DTOs.Seguridad.Auth;

namespace GIRA.Application.Interfaces.Services.Seguridad.Auth;

/// <summary>
/// Casos de uso de autenticación del personal.
/// </summary>
public interface IAuthService
{
    // GI-95: Inicio de sesión (Luis).
    Task<OperationResult<LoginResponse>> LoginAsync(
        LoginRequest request,
        string ip,
        CancellationToken cancellationToken = default);

    // GI-96: Renovar la sesión mediante rotación del refresh token.
    Task<OperationResult<LoginResponse>> RefreshAsync(
        string refreshToken,
        string ip,
        CancellationToken cancellationToken = default);

    // GI-96: Cerrar la sesión revocando el refresh token.
    Task<OperationResult> LogoutAsync(
        Guid usuarioId,
        string refreshToken,
        CancellationToken cancellationToken = default);

    // GI-143: Cambiar la contraseña del usuario autenticado.
    Task<OperationResult> CambiarPasswordAsync(
        Guid usuarioId,
        CambiarPasswordRequest request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// GI-215: Registra una solicitud de recuperación de contraseña para que un Administrador
    /// la atienda. Devuelve éxito aunque el correo no exista o la cuenta esté desactivada:
    /// la respuesta nunca revela si el correo está registrado (GI-212).
    /// </summary>
    Task<OperationResult> OlvidePasswordAsync(
        OlvidePasswordRequest request,
        CancellationToken cancellationToken = default);
}
