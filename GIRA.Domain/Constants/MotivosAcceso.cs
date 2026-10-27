namespace GIRA.Domain.Constants;

public static class MotivosAcceso
{
    /// <summary>El correo no corresponde a ningún usuario.</summary>
    public const string UsuarioNoExiste = "UsuarioNoExiste";

    /// <summary>El usuario existe pero está desactivado.</summary>
    public const string Inactivo = "Inactivo";

    /// <summary>La cuenta está bloqueada por intentos fallidos (RN-SEG-03).</summary>
    public const string Bloqueado = "Bloqueado";

    /// <summary>La contraseña no coincide.</summary>
    public const string PasswordIncorrecta = "PasswordIncorrecta";
}
