namespace GIRA.Application.DTOs.Seguridad.Usuarios;

public record UsuarioFiltroRequest(
    string? Rol,
    bool? Activo,
    int Page = 1,
    int PageSize = 20);
