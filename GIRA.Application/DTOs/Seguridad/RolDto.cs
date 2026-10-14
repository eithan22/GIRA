namespace GIRA.Application.DTOs.Seguridad;

/// <summary>Rol del sistema para el selector de roles (GET /api/roles).</summary>
public record RolDto(Guid Id, string Nombre, string? Descripcion);
