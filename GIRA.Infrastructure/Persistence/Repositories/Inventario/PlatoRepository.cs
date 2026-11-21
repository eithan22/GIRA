using GIRA.Application.Interfaces.Repository.Inventario;
using GIRA.Domain.Entities.Inventario;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Inventario;

/// <summary>Platos con su categoría y su receta.</summary>
public class PlatoRepository : IPlatoRepository
{
    private readonly GiraDbContext _contexto;

    /// <summary>Crea el repositorio.</summary>
    public PlatoRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Plato>> ListarAsync(CancellationToken cancellationToken) =>
        await _contexto.Platos
            .AsNoTracking()
            .Include(p => p.CategoriaPlato)
            .Include(p => p.Receta).ThenInclude(r => r!.Detalles)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<Plato?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        _contexto.Platos
            .Include(p => p.CategoriaPlato)
            .Include(p => p.Receta).ThenInclude(r => r!.Detalles).ThenInclude(d => d.Insumo).ThenInclude(i => i!.UnidadMedida)
            .FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Plato>> ListarActivosConRecetaAsync(CancellationToken cancellationToken) =>
        await _contexto.Platos
            .AsNoTracking()
            .Include(p => p.Receta).ThenInclude(r => r!.Detalles)
            .Where(p => p.Activo && p.Receta != null)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken)
    {
        var n = nombre.Trim().ToLower();
        return _contexto.Platos.AnyAsync(p => p.Nombre.ToLower() == n && (excluirId == null || p.Id != excluirId), cancellationToken);
    }

    /// <inheritdoc />
    public void Agregar(Plato plato) => _contexto.Platos.Add(plato);

    /// <inheritdoc />
    public void QuitarLineaReceta(RecetaDetalle detalle) => _contexto.RecetaDetalles.Remove(detalle);

    /// <inheritdoc />
    public Task GuardarCambiosAsync(CancellationToken cancellationToken) => GuardadoInventario.GuardarAsync(_contexto, cancellationToken);
}
