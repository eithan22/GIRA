using GIRA.Application.DTOs.Configuracion;

namespace GIRA.Application.Interfaces.Services.Configuracion;

/// <summary>Entrega en tiempo real (SignalR). Lo implementa GIRA.Api, que es quien tiene el hub.</summary>
public interface IEmisorNotificaciones
{
    /// <summary>Envía la notificación a las conexiones abiertas del usuario; si no tiene ninguna, no pasa nada.</summary>
    Task EnviarAsync(Guid usuarioId, NotificacionDto notificacion, CancellationToken cancellationToken);
}
