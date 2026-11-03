namespace GIRA.Domain.Entities.Seguridad;

/// <summary>
/// Solicitud de recuperación de contraseña hecha por un usuario que olvidó la suya.
/// No referencia a <c>Usuario</c> directamente porque esa entidad vive en
/// GIRA.Infrastructure (Identity); solo guarda su identificador.
/// No guarda token ni código: el restablecimiento lo hace un Administrador a mano
/// (GI-213), y la fila se borra cuando atiende o descarta la solicitud.
/// </summary>
public class SolicitudRecuperacionPassword
{
    /// <summary>Identificador único de la solicitud.</summary>
    public Guid Id { get; set; }

    /// <summary>Identificador del usuario que pidió recuperar su contraseña.</summary>
    public Guid UsuarioId { get; set; }

    /// <summary>Fecha y hora, en UTC, en que se hizo la solicitud.</summary>
    public DateTime FechaSolicitud { get; set; }
}
