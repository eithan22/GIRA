using GIRA.Application.Common;
using GIRA.Domain.Entities.Inventario;
using GIRA.Domain.Enums.Inventario;

namespace GIRA.Application.Interfaces.Repository.Inventario;

/// <summary>Movimiento del kardex con el nombre de quien lo registró (null si fue el sistema).</summary>
public sealed record MovimientoLectura(MovimientoInventario Movimiento, string? Usuario);

/// <summary>Movimientos de inventario (kardex).</summary>
public interface IMovimientoInventarioRepository
{
    /// <summary>Agrega un movimiento.</summary>
    void Agregar(MovimientoInventario movimiento);
    /// <summary>True si la visita ya generó salidas teóricas.</summary>
    Task<bool> ExisteDeVisitaAsync(Guid visitaId, CancellationToken cancellationToken);
    /// <summary>Saldo del insumo justo antes de <paramref name="desdeUtc"/> (0 si no había movimientos).</summary>
    Task<decimal> SaldoAntesDeAsync(Guid insumoId, DateTime desdeUtc, CancellationToken cancellationToken);
    /// <summary>Movimientos del insumo en [desdeUtc, hastaUtc), del más viejo al más nuevo.</summary>
    Task<PagedResult<MovimientoLectura>> ListarAsync(Guid insumoId, DateTime desdeUtc, DateTime hastaUtc, int page, int pageSize, CancellationToken cancellationToken);
    /// <summary>Suma de cantidades (con signo) por tipo en [desdeUtc, hastaUtc).</summary>
    Task<IReadOnlyDictionary<TipoMovimientoInventario, decimal>> TotalesPorTipoAsync(Guid insumoId, DateTime desdeUtc, DateTime hastaUtc, CancellationToken cancellationToken);
    /// <summary>Guarda. Lanza InventarioDuplicadoException o InventarioConflictoConcurrenciaException según el caso.</summary>
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}
