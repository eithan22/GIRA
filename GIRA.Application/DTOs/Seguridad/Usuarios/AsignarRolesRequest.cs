namespace GIRA.Application.DTOs.Seguridad.Usuarios;

/// <summary>Roles que tendrá el usuario; reemplazan a los actuales (PUT /api/usuarios/{id}/roles).</summary>
public record AsignarRolesRequest(IReadOnlyList<string> Roles);
