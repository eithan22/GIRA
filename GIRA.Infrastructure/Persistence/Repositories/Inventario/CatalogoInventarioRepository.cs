using GIRA.Application.Interfaces.Repository.Inventario;
using GIRA.Domain.Entities.Inventario;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Inventario;

/// <summary>Unidades, categorías de insumo y de plato, y proveedores.</summary>
public class CatalogoInventarioRepository : ICatalogoInventarioRepository
{
    private readonly GiraDbContext _contexto;

    /// <summary>Crea el repositorio.</summary>
    public CatalogoInventarioRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<UnidadMedida>> ListarUnidadesAsync(CancellationToken cancellationToken) =>
        await _contexto.UnidadesMedida.AsNoTracking().OrderBy(u => u.Nombre).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<UnidadMedida?> BuscarUnidadAsync(Guid id, CancellationToken cancellationToken) =>
        _contexto.UnidadesMedida.FirstOrDefaultAsync(u => u.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<bool> ExisteUnidadAsync(string nombre, string abreviatura, Guid? excluirId, CancellationToken cancellationToken)
    {
        var (n, a) = (nombre.Trim().ToLower(), abreviatura.Trim().ToLower());
        return _contexto.UnidadesMedida.AnyAsync(
            u => (u.Nombre.ToLower() == n || u.Abreviatura.ToLower() == a) && (excluirId == null || u.Id != excluirId),
            cancellationToken);
    }

    /// <inheritdoc />
    public void AgregarUnidad(UnidadMedida unidad) => _contexto.UnidadesMedida.Add(unidad);

    /// <inheritdoc />
    public async Task<IReadOnlyList<CategoriaInsumo>> ListarCategoriasInsumoAsync(CancellationToken cancellationToken) =>
        await _contexto.CategoriasInsumo.AsNoTracking().OrderBy(c => c.Nombre).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<CategoriaInsumo?> BuscarCategoriaInsumoAsync(Guid id, CancellationToken cancellationToken) =>
        _contexto.CategoriasInsumo.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<bool> ExisteCategoriaInsumoAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken)
    {
        var n = nombre.Trim().ToLower();
        return _contexto.CategoriasInsumo.AnyAsync(c => c.Nombre.ToLower() == n && (excluirId == null || c.Id != excluirId), cancellationToken);
    }

    /// <inheritdoc />
    public void AgregarCategoriaInsumo(CategoriaInsumo categoria) => _contexto.CategoriasInsumo.Add(categoria);

    /// <inheritdoc />
    public async Task<IReadOnlyList<CategoriaPlato>> ListarCategoriasPlatoAsync(CancellationToken cancellationToken) =>
        await _contexto.CategoriasPlato.AsNoTracking().OrderBy(c => c.Nombre).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<CategoriaPlato?> BuscarCategoriaPlatoAsync(Guid id, CancellationToken cancellationToken) =>
        _contexto.CategoriasPlato.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<bool> ExisteCategoriaPlatoAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken)
    {
        var n = nombre.Trim().ToLower();
        return _contexto.CategoriasPlato.AnyAsync(c => c.Nombre.ToLower() == n && (excluirId == null || c.Id != excluirId), cancellationToken);
    }

    /// <inheritdoc />
    public void AgregarCategoriaPlato(CategoriaPlato categoria) => _contexto.CategoriasPlato.Add(categoria);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Proveedor>> ListarProveedoresAsync(CancellationToken cancellationToken) =>
        await _contexto.Proveedores.AsNoTracking().OrderBy(p => p.Nombre).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<Proveedor?> BuscarProveedorAsync(Guid id, CancellationToken cancellationToken) =>
        _contexto.Proveedores.FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <inheritdoc />
    public Task<bool> ExisteProveedorAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken)
    {
        var n = nombre.Trim().ToLower();
        return _contexto.Proveedores.AnyAsync(p => p.Nombre.ToLower() == n && (excluirId == null || p.Id != excluirId), cancellationToken);
    }

    /// <inheritdoc />
    public void AgregarProveedor(Proveedor proveedor) => _contexto.Proveedores.Add(proveedor);

    /// <inheritdoc />
    public Task GuardarCambiosAsync(CancellationToken cancellationToken) => GuardadoInventario.GuardarAsync(_contexto, cancellationToken);
}
