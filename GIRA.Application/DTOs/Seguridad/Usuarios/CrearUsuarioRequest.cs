namespace GIRA.Application.DTOs.Seguridad.Usuarios;

public record CrearUsuarioRequest(
    string Email,
    string NombreCompleto,
    Guid? EmpleadoId);
