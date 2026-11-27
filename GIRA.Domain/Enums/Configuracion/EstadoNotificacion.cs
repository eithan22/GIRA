namespace GIRA.Domain.Enums.Configuracion;

/// <summary>Resultado del envío en tiempo real de una notificación. Se guarda como número: no cambiarlos sin migración.</summary>
public enum EstadoNotificacion
{
    /// <summary>Guardada, todavía sin enviar.</summary>
    Pendiente = 0,
    /// <summary>Enviada por SignalR.</summary>
    Enviada = 1,
    /// <summary>El envío falló; sigue guardada y el usuario la ve en su bandeja.</summary>
    Fallida = 2
}
