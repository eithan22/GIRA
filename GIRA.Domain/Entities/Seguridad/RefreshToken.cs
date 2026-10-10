namespace GIRA.Domain.Entities.Seguridad;

/// <summary>
/// Token de renovación de sesión, asociado a un usuario. No referencia a
/// <c>Usuario</c> directamente porque esa entidad vive en GIRA.Infrastructure
/// (Identity); solo guarda su identificador.
/// </summary>
public class RefreshToken
{
    /// <summary>Identificador único del token.</summary>
    public Guid Id { get; set; }

    /// <summary>Identificador del usuario propietario del token.</summary>
    public Guid UsuarioId { get; set; }

    /// <summary>Hash del token (nunca se guarda el valor en texto plano).</summary>
    public string TokenHash { get; set; } = string.Empty;

    /// <summary>Fecha y hora, en UTC, en que se emitió el token.</summary>
    public DateTime FechaCreacion { get; set; }

    /// <summary>Fecha y hora, en UTC, en que el token deja de ser válido.</summary>
    public DateTime FechaExpiracion { get; set; }

    /// <summary>Dirección IP desde la que se emitió el token.</summary>
    public string IpCreacion { get; set; } = string.Empty;

    /// <summary>Fecha y hora, en UTC, en que el token fue revocado; null si sigue vigente.</summary>
    public DateTime? FechaRevocacion { get; set; }

    /// <summary>Identificador del token que reemplazó a este tras una rotación; null si no fue reemplazado.</summary>
    public Guid? ReemplazadoPorId { get; set; }
}
