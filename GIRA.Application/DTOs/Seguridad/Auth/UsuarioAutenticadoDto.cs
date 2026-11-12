
namespace GIRA.Application.DTOs.Seguridad.Auth;

/// <summary>Datos básicos del usuario que inició sesión.</summary>
public class UsuarioAutenticadoDto
{
    /// <summary>Identificador del usuario.</summary>
    public Guid Id { get; set; }

    /// <summary>Correo del usuario.</summary>
    public string Email { get; set; } = string.Empty;

    /// <summary>Nombre completo de la persona.</summary>
    public string NombreCompleto { get; set; } = string.Empty;

    /// <summary>Roles asignados al usuario.</summary>
    public IReadOnlyList<string> Roles { get; set; } = [];

    /// <summary>Empleado vinculado; null si el usuario no corresponde a un empleado.</summary>
    public Guid? EmpleadoId { get; set; }

    /// <summary>Indica si el usuario debe cambiar su contraseña temporal.</summary>
    public bool DebeCambiarPassword { get; set; }
}
