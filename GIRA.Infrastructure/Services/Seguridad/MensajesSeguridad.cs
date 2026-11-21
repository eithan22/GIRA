namespace GIRA.Infrastructure.Services.Seguridad;

/// <summary>Mensajes de error repetidos entre los servicios de Seguridad, con el texto exacto que ya mostraban.</summary>
internal static class MensajesSeguridad
{
    public const string UsuarioNoExiste = "El usuario no existe.";
    public const string UsuarioNoAutorizado = "Usuario no autorizado.";
    public const string TokenRenovacionInvalido = "Token de renovación inválido.";
}
