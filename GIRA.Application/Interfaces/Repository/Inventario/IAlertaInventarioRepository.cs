using GIRA.Application.Common;
using GIRA.Domain.Entities.Inventario;
using GIRA.Domain.Enums.Inventario;

namespace GIRA.Application.Interfaces.Repository.Inventario;

/// <summary>Alerta con su insumo (y unidad) y el nombre de quien la cerró.</summary>
public sealed record AlertaLectura(AlertaInventario Alerta, string? AtendidaPor);

/// <summary>Alertas de inventario.</summary>
public interface IAlertaInventarioRepository
{
    /// <summary>Alertas abiertas de esos insumos (con seguimiento, para poder cerrarlas).</summary>
    Task<IReadOnlyList<AlertaInventario>> ListarAbiertasAsync(IReadOnlyCollection<Guid> insumoIds, CancellationToken cancellationToken);
    /// <summary>Alertas por estado (null = todas), de la más nueva a la más vieja.</summary>
    Task<PagedResult<AlertaLectura>> ListarAsync(EstadoAlertaInventario? estado, int page, int pageSize, CancellationToken cancellationToken);
    /// <summary>Alerta por id; null si no existe.</summary>
    Task<AlertaInventario?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>Alerta por id con insumo y nombre de quien la cerró; null si no existe.</summary>
    Task<AlertaLectura?> ObtenerLecturaAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>Agrega una alerta.</summary>
    void Agregar(AlertaInventario alerta);
    /// <summary>Guarda. Lanza InventarioDuplicadoException o InventarioConflictoConcurrenciaException según el caso.</summary>
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}
