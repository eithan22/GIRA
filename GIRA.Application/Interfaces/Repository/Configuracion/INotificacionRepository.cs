using GIRA.Domain.Entities.Configuracion;

namespace GIRA.Application.Interfaces.Repository.Configuracion;

/// <summary>Acceso a las notificaciones in-app (tabla <c>configuracion.Notificacion</c>).</summary>
public interface INotificacionRepository
{
    /// <summary>Agrega notificaciones nuevas (se guardan con <see cref="GuardarCambiosAsync"/>).</summary>
    void Agregar(IEnumerable<Notificacion> notificaciones);

    /// <summary>
    /// Las <paramref name="cantidad"/> más recientes del usuario, sin seguimiento de cambios. Con <paramref name="leidas"/>
    /// filtra por leídas (true) o no leídas (false); null trae todas.
    /// </summary>
    Task<IReadOnlyList<Notificacion>> ListarDelUsuarioAsync(Guid usuarioId, bool? leidas, int cantidad, CancellationToken cancellationToken);

    /// <summary>Cuántas no leídas tiene el usuario.</summary>
    Task<int> ContarNoLeidasAsync(Guid usuarioId, CancellationToken cancellationToken);

    /// <summary>Notificación del usuario por id, con seguimiento de cambios; null si no existe o es de otro usuario.</summary>
    Task<Notificacion?> BuscarDelUsuarioAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken);

    /// <summary>Guarda los cambios pendientes.</summary>
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}
