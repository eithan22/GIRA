
namespace GIRA.Application.DTOs.Seguridad.Auth;

/// <summary>Solicitud para renovar una sesión.</summary>
public class RefreshTokenRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
