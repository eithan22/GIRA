namespace GIRA.Application.DTOs.Seguridad;

/// <summary>Credenciales para iniciar sesión (POST /api/auth/login).</summary>
public class LoginRequest
{
    /// <summary>Correo del usuario.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Contraseña en texto plano; nunca se guarda ni se registra.</summary>
    public string Password { get; set; } = string.Empty;
}
