
namespace GIRA.Application.DTOs.Seguridad.Auth;

/// <summary>Solicitud para cerrar una sesión.</summary>
public class LogoutRequest
{
    public string RefreshToken { get; set; } = string.Empty;
}
