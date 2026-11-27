using GIRA.Domain.Base;
using GIRA.Domain.Enums.Configuracion;

namespace GIRA.Domain.Entities.Configuracion;

/// <summary>
/// Aviso dentro de la aplicación para un usuario del personal (SRS 5.6, RF-CFG-05). Se guarda siempre, para que lo vea
/// al iniciar sesión, y además se intenta entregar en tiempo real (RN-CFG-05). La fecha de creación es <c>CreatedAt</c>.
/// </summary>
public class Notificacion : BaseEntity
{
    /// <summary>Usuario que recibe el aviso.</summary>
    public Guid UsuarioId { get; set; }

    /// <summary>Por qué le llegó: el rol (por ejemplo «Gerente») o «Empleado» si es su propio turno. Solo informativo.</summary>
    public string Destinatario { get; set; } = string.Empty;

    /// <summary>Canal de entrega; hoy solo dentro de la aplicación.</summary>
    public CanalNotificacion Canal { get; set; } = CanalNotificacion.InApp;

    /// <summary>Tipo de aviso (por ejemplo «AlertaInventario» o «TurnoAsignado»), para el ícono y los filtros.</summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary>Título corto.</summary>
    public string Titulo { get; set; } = string.Empty;

    /// <summary>Texto del aviso.</summary>
    public string Mensaje { get; set; } = string.Empty;

    /// <summary>True cuando el usuario la marcó como leída.</summary>
    public bool Leida { get; set; }

    /// <summary>Resultado del envío en tiempo real.</summary>
    public EstadoNotificacion Estado { get; set; } = EstadoNotificacion.Pendiente;

    /// <summary>Intentos de envío en tiempo real.</summary>
    public int Intentos { get; set; }

    /// <summary>Fecha y hora, en UTC, del envío en tiempo real; null si no se envió.</summary>
    public DateTime? FechaEnvio { get; set; }
}
