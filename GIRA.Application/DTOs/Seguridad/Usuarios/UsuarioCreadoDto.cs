namespace GIRA.Application.DTOs.Seguridad.Usuarios;

/// <summary>Respuesta al crear: la contraseña temporal se muestra solo esta vez.</summary>
public record UsuarioCreadoDto(UsuarioDto Usuario, string PasswordTemporal);
