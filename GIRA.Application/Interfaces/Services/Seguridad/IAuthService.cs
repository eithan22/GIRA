using GIRA.Application.Common;
using GIRA.Application.DTOs.Seguridad;

namespace GIRA.Application.Interfaces.Services.Seguridad;

/// <summary>Casos de uso de autenticación del personal.</summary>
public interface IAuthService
{
    /// <summary>
    /// Valida las credenciales y, si son correctas, emite el access token y el refresh token.
    /// Cualquier fallo de credenciales devuelve el mismo error <see cref="ErrorType.Unauthorized"/>.
    /// </summary>
    Task<OperationResult<LoginResponse>> LoginAsync(LoginRequest request, string ip, CancellationToken cancellationToken = default);
}
