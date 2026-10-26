namespace GIRA.Application.DTOs.Seguridad;

/// <summary>Respuesta de un inicio de sesión exitoso.</summary>
public class LoginResponse
{
    /// <summary>Access token JWT con los roles del usuario.</summary>
    public string AccessToken { get; set; } = string.Empty;

    /// <summary>Fecha y hora, en UTC, en que vence el access token.</summary>
    public DateTime AccessTokenExpiraEn { get; set; }

    /// <summary>Refresh token en claro; solo se entrega aquí, en la base se guarda su hash.</summary>
    public string RefreshToken { get; set; } = string.Empty;

    /// <summary>Fecha y hora, en UTC, en que vence el refresh token.</summary>
    public DateTime RefreshTokenExpiraEn { get; set; }

    /// <summary>Datos básicos del usuario autenticado.</summary>
    public UsuarioAutenticadoDto Usuario { get; set; } = new();
}
