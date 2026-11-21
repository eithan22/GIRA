using GIRA.Domain.Entities.Inventario;

namespace GIRA.Application.Interfaces.Repository.Inventario;

/// <summary>Platos con su receta (un agregado: Plato, Receta y RecetaDetalle).</summary>
public interface IPlatoRepository
{
    /// <summary>Todos los platos con categoría y receta.</summary>
    Task<IReadOnlyList<Plato>> ListarAsync(CancellationToken cancellationToken);
    /// <summary>Plato con categoría y receta (líneas con insumo y unidad); null si no existe.</summary>
    Task<Plato?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>Platos activos con receta, solo lectura, para calcular el consumo.</summary>
    Task<IReadOnlyList<Plato>> ListarActivosConRecetaAsync(CancellationToken cancellationToken);
    /// <summary>True si otro plato ya usa ese nombre.</summary>
    Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken);
    /// <summary>Agrega un plato.</summary>
    void Agregar(Plato plato);

    /// <summary>Marca una línea de receta como borrada (borrado lógico al guardar).</summary>
    void QuitarLineaReceta(RecetaDetalle detalle);
    /// <summary>Guarda. Lanza InventarioDuplicadoException o InventarioConflictoConcurrenciaException según el caso.</summary>
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}
