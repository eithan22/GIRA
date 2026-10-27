namespace GIRA.Infrastructure.Identity;

/// <summary>
/// Nombres de los claims del access token. La API los lee tal cual (sin traducirlos a
/// los URI largos de ClaimTypes), así que para obtener el usuario autenticado se usa
/// <c>User.FindFirst(TiposClaim.UsuarioId)</c>.
/// </summary>
public static class TiposClaim
{
    /// <summary>Identificador del usuario.</summary>
    public const string UsuarioId = "sub";

    /// <summary>Correo del usuario.</summary>
    public const string Email = "email";

    /// <summary>Nombre completo del usuario.</summary>
    public const string Nombre = "name";

    /// <summary>Rol del usuario; hay un claim por cada rol.</summary>
    public const string Rol = "role";

    /// <summary>Identificador único del token.</summary>
    public const string TokenId = "jti";
}
