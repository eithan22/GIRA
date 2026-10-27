using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace GIRA.Infrastructure.Identity;

/// <summary>
/// Configuración de los tokens de sesión (sección "Jwt"). Emisor, audiencia y duraciones
/// van en appsettings.json; la clave de firma va solo en User Secrets (<c>Jwt:Key</c>).
/// </summary>
public class JwtSettings
{
    /// <summary>Nombre de la sección en la configuración.</summary>
    public const string Seccion = "Jwt";

    /// <summary>Emisor del token (claim "iss").</summary>
    public string Issuer { get; set; } = string.Empty;

    /// <summary>Destinatario del token (claim "aud").</summary>
    public string Audience { get; set; } = string.Empty;

    /// <summary>Clave secreta para firmar con HMAC-SHA256; mínimo 32 caracteres.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Duración del access token en minutos (RN-SEG-04).</summary>
    public int AccessTokenMinutos { get; set; } = 30;

    /// <summary>Duración del refresh token en días (RN-SEG-04).</summary>
    public int RefreshTokenDias { get; set; } = 7;

    /// <summary>Clave de firma lista para emitir y validar tokens.</summary>
    public SymmetricSecurityKey ClaveFirma() => new(Encoding.UTF8.GetBytes(Key));

    /// <summary>Lee la sección "Jwt" y lanza una excepción clara si falta algún valor obligatorio.</summary>
    public static JwtSettings Leer(IConfiguration configuration)
    {
        var settings = configuration.GetSection(Seccion).Get<JwtSettings>() ?? new JwtSettings();

        if (string.IsNullOrWhiteSpace(settings.Key))
        {
            throw new InvalidOperationException(
                "Falta 'Jwt:Key' en la configuración. Configúrala en User Secrets con la clave 'Jwt:Key'.");
        }

        // HMAC-SHA256 exige una clave de al menos 256 bits.
        if (Encoding.UTF8.GetByteCount(settings.Key) < 32)
        {
            throw new InvalidOperationException("'Jwt:Key' debe tener al menos 32 caracteres.");
        }

        if (string.IsNullOrWhiteSpace(settings.Issuer) || string.IsNullOrWhiteSpace(settings.Audience))
        {
            throw new InvalidOperationException("Faltan 'Jwt:Issuer' o 'Jwt:Audience' en appsettings.json.");
        }

        return settings;
    }
}
