namespace GIRA.Application.DTOs.Seguridad;

public record ActualizarUsuarioRequest(
    string Email,
    string NombreCompleto,
    Guid? EmpleadoId);
