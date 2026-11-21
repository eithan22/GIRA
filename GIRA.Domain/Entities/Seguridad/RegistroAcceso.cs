namespace GIRA.Domain.Entities.Seguridad;

/// <summary>
/// Registro de un intento de inicio de sesión, exitoso o no. No referencia a
/// <c>Usuario</c> directamente porque esa entidad vive en GIRA.Infrastructure
/// (Identity) y porque el intento puede corresponder a un correo que no existe.
/// </summary>
public class RegistroAcceso
{
    /// <summary>Identificador único del registro.</summary>
    public Guid Id { get; set; }

    /// <summary>Identificador del usuario que intentó iniciar sesión; null si el correo no corresponde a ningún usuario.</summary>
    public Guid? UsuarioId { get; set; }

    /// <summary>Correo con el que se intentó iniciar sesión, tal como se escribió.</summary>
    public string CorreoIntentado { get; set; } = string.Empty;

    /// <summary>Fecha y hora, en UTC, del intento.</summary>
    public DateTime Fecha { get; set; }

    /// <summary>Dirección IP desde la que se intentó iniciar sesión.</summary>
    public string Ip { get; set; } = string.Empty;

    /// <summary>Indica si el inicio de sesión fue exitoso.</summary>
    public bool Exitoso { get; set; }

    /// <summary>Motivo del fallo (por ejemplo, contraseña incorrecta o cuenta bloqueada); null si fue exitoso.</summary>
    public string? Motivo { get; set; }
}
