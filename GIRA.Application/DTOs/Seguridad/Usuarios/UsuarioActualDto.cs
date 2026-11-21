
namespace GIRA.Application.DTOs.Seguridad.Usuarios;

/// <summary>Datos del usuario autenticado (GET /api/auth/me).</summary>
public record UsuarioActualDto(
    Guid Id,
    string Email,
    string NombreCompleto,
    IReadOnlyList<string> Roles,
    Guid? EmpleadoId,
    bool DebeCambiarPassword);
