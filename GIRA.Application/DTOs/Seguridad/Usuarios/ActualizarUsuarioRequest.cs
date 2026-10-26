namespace GIRA.Application.DTOs.Seguridad.Usuarios;

public record ActualizarUsuarioRequest(
    string Email,
    string NombreCompleto,
    Guid? EmpleadoId);
