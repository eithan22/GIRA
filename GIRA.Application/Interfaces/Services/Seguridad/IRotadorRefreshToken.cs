using GIRA.Application.Common;
using GIRA.Application.DTOs.Seguridad.Auth;

namespace GIRA.Application.Interfaces.Services.Seguridad;

/// <summary>
/// Valida y rota un refresh token (GI-119): detecta reutilización de un token ya
/// reemplazado, revoca el token consumido y emite su reemplazo, todo dentro de una
/// única transacción serializable.
/// </summary>
public interface IRotadorRefreshToken
{
    /// <summary>Intercambia un refresh token vigente por uno nuevo, revocando el anterior.</summary>
    Task<OperationResult<LoginResponse>> RotarAsync(
        string refreshToken,
        string ip,
        CancellationToken cancellationToken = default);
}
