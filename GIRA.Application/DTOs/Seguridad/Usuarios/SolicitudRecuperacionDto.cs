namespace GIRA.Application.DTOs.Seguridad.Usuarios;

/// <summary>
/// Solicitud de recuperación de contraseña pendiente, con los datos del usuario
/// que la pidió, para que el Administrador la atienda (GI-213).
/// </summary>
/// <param name="UsuarioId">Identificador del usuario que pidió recuperar su contraseña.</param>
/// <param name="NombreCompleto">Nombre completo del usuario.</param>
/// <param name="Email">Correo de la cuenta.</param>
/// <param name="FechaSolicitud">Fecha y hora, en UTC, en que se hizo la solicitud.</param>
public record SolicitudRecuperacionDto(
    Guid UsuarioId,
    string NombreCompleto,
    string Email,
    DateTime FechaSolicitud);
