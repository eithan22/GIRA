using GIRA.Application.Common;
using GIRA.Application.DTOs.Inventario;
using GIRA.Domain.Entities.Inventario;

namespace GIRA.Application.Interfaces.Repository.Inventario;

/// <summary>Lote con saldo que vence pronto, con lo necesario para el aviso.</summary>
public sealed record LotePorVencer(Guid LoteId, Guid InsumoId, decimal Cantidad, DateOnly FechaVencimiento, string Unidad, decimal StockInsumo);

/// <summary>Insumos y sus lotes. Las consultas de insumos traen categoría, unidad y proveedor.</summary>
public interface IInsumoRepository
{
    /// <summary>Insumo por id; null si no existe.</summary>
    Task<Insumo?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>Insumos con esos ids (los que existan).</summary>
    Task<IReadOnlyList<Insumo>> BuscarPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    /// <summary>Todos los insumos activos.</summary>
    Task<IReadOnlyList<Insumo>> ListarActivosAsync(CancellationToken cancellationToken);
    /// <summary>Catálogo paginado, por nombre. <paramref name="nivel"/> filtra solo activos.</summary>
    Task<PagedResult<Insumo>> ListarAsync(string? buscar, Guid? categoriaId, string? nivel, bool incluirInactivos, int page, int pageSize, CancellationToken cancellationToken);
    /// <summary>Activos y cuántos están en nivel bajo o crítico.</summary>
    Task<ResumenInsumosDto> ResumenAsync(CancellationToken cancellationToken);
    /// <summary>True si otro insumo ya usa ese nombre.</summary>
    Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken);
    /// <summary>Nombres de los platos activos cuya receta usa el insumo (RN-INV-10).</summary>
    Task<IReadOnlyList<string>> PlatosActivosQueLoUsanAsync(Guid insumoId, CancellationToken cancellationToken);
    /// <summary>Agrega un insumo.</summary>
    void Agregar(Insumo insumo);

    /// <summary>Lotes del insumo con saldo, del que vence primero al último.</summary>
    Task<IReadOnlyList<LoteInsumo>> ListarLotesConSaldoAsync(Guid insumoId, CancellationToken cancellationToken);
    /// <summary>Lotes con saldo que vencen hasta <paramref name="hasta"/>, de insumos activos y perecederos.</summary>
    Task<IReadOnlyList<LotePorVencer>> ListarLotesPorVencerAsync(DateOnly hasta, CancellationToken cancellationToken);
    /// <summary>Agrega un lote.</summary>
    void AgregarLote(LoteInsumo lote);

    /// <summary>Guarda. Lanza InventarioDuplicadoException o InventarioConflictoConcurrenciaException según el caso.</summary>
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}
