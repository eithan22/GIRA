using GIRA.Application.Common;
using GIRA.Application.DTOs.Configuracion;

namespace GIRA.Application.Interfaces.Services.Configuracion;

/// <summary>Notificaciones in-app del personal (SRS 5.6, RF-CFG-05 y RF-CFG-06).</summary>
public interface INotificacionService
{
    /// <summary>
    /// Guarda una notificación por destinatario (un usuario repetido recibe una sola) y la envía en tiempo real.
    /// Si el envío falla, queda guardada como Fallida y el usuario la ve en su bandeja (RN-CFG-05).
    /// </summary>
    Task NotificarAsync(IReadOnlyCollection<DestinatarioNotificacion> destinatarios, AvisoNuevo aviso, CancellationToken cancellationToken);

    /// <summary>Las 50 más recientes del usuario (filtradas por <paramref name="leidas"/> si viene) y su total de no leídas.</summary>
    Task<BandejaNotificacionesDto> ListarMiasAsync(Guid usuarioId, bool? leidas, CancellationToken cancellationToken);

    /// <summary>Marca como leída una notificación propia. NotFound si no existe o es de otro usuario.</summary>
    Task<OperationResult> MarcarLeidaAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken);
}
