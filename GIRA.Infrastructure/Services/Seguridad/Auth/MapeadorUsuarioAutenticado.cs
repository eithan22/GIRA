using GIRA.Application.DTOs.Seguridad.Auth;
using GIRA.Infrastructure.Identity;

namespace GIRA.Infrastructure.Services.Seguridad.Auth;

/// <summary>
/// Convierte un <see cref="Usuario"/> de Identity en su DTO de autenticación.
/// Vive en Infrastructure porque <see cref="Usuario"/> no es visible desde Application.
/// </summary>
internal static class MapeadorUsuarioAutenticado
{
    /// <summary>Construye el DTO de usuario autenticado a partir de la entidad y sus roles.</summary>
    public static UsuarioAutenticadoDto ADto(Usuario usuario, IList<string> roles) => new()
    {
        Id = usuario.Id,
        Email = usuario.Email!,
        NombreCompleto = usuario.NombreCompleto,
        Roles = roles.ToList(),
        EmpleadoId = usuario.EmpleadoId,
        DebeCambiarPassword = usuario.DebeCambiarPassword
    };
}
