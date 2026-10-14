namespace GIRA.Application.DTOs.Seguridad;

public record CrearUsuarioRequest(
    string Email,
    string NombreCompleto,
    Guid? EmpleadoId);
