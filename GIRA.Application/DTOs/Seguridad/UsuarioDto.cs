namespace GIRA.Application.DTOs.Seguridad;

public record UsuarioDto(
    Guid Id,
    string Email,
    string NombreCompleto,
    bool Activo,
    Guid? EmpleadoId,
    DateTime? UltimoAcceso,
    IReadOnlyList<string> Roles);
