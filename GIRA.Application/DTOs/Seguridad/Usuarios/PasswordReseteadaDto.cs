namespace GIRA.Application.DTOs.Seguridad.Usuarios;

/// <summary>Respuesta al resetear: la contraseña temporal se muestra solo esta vez.</summary>
public record PasswordReseteadaDto(UsuarioDto Usuario, string PasswordTemporal);
