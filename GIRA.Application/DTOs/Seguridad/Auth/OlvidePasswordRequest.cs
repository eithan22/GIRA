namespace GIRA.Application.DTOs.Seguridad.Auth;

/// <summary>
/// Solicitud de recuperación de contraseña: solo el correo de la cuenta.
/// La respuesta es siempre la misma, exista o no el correo (GI-212).
/// </summary>
public class OlvidePasswordRequest
{
    /// <summary>Correo de la cuenta cuya contraseña se olvidó.</summary>
    public string Email { get; set; } = string.Empty;
}
