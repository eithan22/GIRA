using Microsoft.AspNetCore.Identity;

namespace GIRA.Infrastructure.Identity;

/// <summary>
/// Usuario del sistema GIRA. Extiende el usuario de ASP.NET Core Identity,
/// que ya provee hash de contraseña, bloqueo por intentos fallidos y
/// normalización de correo/usuario.
/// </summary>
public class Usuario : IdentityUser<Guid>
{
    /// <summary>Nombre completo de la persona.</summary>
    public string NombreCompleto { get; set; } = string.Empty;

    /// <summary>Empleado del módulo de Personal asociado a este usuario; null si el usuario no corresponde a un empleado.</summary>
    public Guid? EmpleadoId { get; set; }

    /// <summary>Indica si el usuario puede iniciar sesión. Se usa para deshabilitar cuentas sin eliminarlas.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Fecha y hora, en UTC, del último inicio de sesión exitoso; null si nunca inició sesión.</summary>
    public DateTime? UltimoAcceso { get; set; }

    /// <summary>Fecha y hora, en UTC, en que se creó la cuenta.</summary>
    public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
}
