using GIRA.Domain.Entities.Inventario;

namespace GIRA.Application.Interfaces.Repository.Inventario;

/// <summary>Catálogos pequeños de Inventario: unidades, categorías de insumo y de plato, y proveedores.</summary>
public interface ICatalogoInventarioRepository
{
    /// <summary>Unidades ordenadas por nombre.</summary>
    Task<IReadOnlyList<UnidadMedida>> ListarUnidadesAsync(CancellationToken cancellationToken);
    /// <summary>Unidad por id; null si no existe.</summary>
    Task<UnidadMedida?> BuscarUnidadAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>True si otra unidad ya usa ese nombre o esa abreviatura.</summary>
    Task<bool> ExisteUnidadAsync(string nombre, string abreviatura, Guid? excluirId, CancellationToken cancellationToken);
    /// <summary>Agrega una unidad.</summary>
    void AgregarUnidad(UnidadMedida unidad);

    /// <summary>Categorías de insumo ordenadas por nombre.</summary>
    Task<IReadOnlyList<CategoriaInsumo>> ListarCategoriasInsumoAsync(CancellationToken cancellationToken);
    /// <summary>Categoría de insumo por id; null si no existe.</summary>
    Task<CategoriaInsumo?> BuscarCategoriaInsumoAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>True si otra categoría de insumo ya usa ese nombre.</summary>
    Task<bool> ExisteCategoriaInsumoAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken);
    /// <summary>Agrega una categoría de insumo.</summary>
    void AgregarCategoriaInsumo(CategoriaInsumo categoria);

    /// <summary>Categorías de plato ordenadas por nombre.</summary>
    Task<IReadOnlyList<CategoriaPlato>> ListarCategoriasPlatoAsync(CancellationToken cancellationToken);
    /// <summary>Categoría de plato por id; null si no existe.</summary>
    Task<CategoriaPlato?> BuscarCategoriaPlatoAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>True si otra categoría de plato ya usa ese nombre.</summary>
    Task<bool> ExisteCategoriaPlatoAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken);
    /// <summary>Agrega una categoría de plato.</summary>
    void AgregarCategoriaPlato(CategoriaPlato categoria);

    /// <summary>Proveedores ordenados por nombre.</summary>
    Task<IReadOnlyList<Proveedor>> ListarProveedoresAsync(CancellationToken cancellationToken);
    /// <summary>Proveedor por id; null si no existe.</summary>
    Task<Proveedor?> BuscarProveedorAsync(Guid id, CancellationToken cancellationToken);
    /// <summary>True si otro proveedor ya usa ese nombre.</summary>
    Task<bool> ExisteProveedorAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken);
    /// <summary>Agrega un proveedor.</summary>
    void AgregarProveedor(Proveedor proveedor);

    /// <summary>Guarda. Lanza InventarioDuplicadoException o InventarioConflictoConcurrenciaException según el caso.</summary>
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}
