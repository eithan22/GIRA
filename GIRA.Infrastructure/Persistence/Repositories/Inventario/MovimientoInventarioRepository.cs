using GIRA.Application.Common;
using GIRA.Application.Interfaces.Repository.Inventario;
using GIRA.Domain.Entities.Inventario;
using GIRA.Domain.Enums.Inventario;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Inventario;

/// <summary>Movimientos de inventario (el kardex).</summary>
public class MovimientoInventarioRepository : IMovimientoInventarioRepository
{
    private readonly GiraDbContext _contexto;

    /// <summary>Crea el repositorio.</summary>
    public MovimientoInventarioRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <inheritdoc />
    public void Agregar(MovimientoInventario movimiento) => _contexto.MovimientosInventario.Add(movimiento);

    /// <inheritdoc />
    public Task<bool> ExisteDeVisitaAsync(Guid visitaId, CancellationToken cancellationToken) =>
        _contexto.MovimientosInventario.AnyAsync(m => m.VisitaId == visitaId, cancellationToken);

    /// <inheritdoc />
    public async Task<decimal> SaldoAntesDeAsync(Guid insumoId, DateTime desdeUtc, CancellationToken cancellationToken) =>
        await _contexto.MovimientosInventario
            .Where(m => m.InsumoId == insumoId && m.Fecha < desdeUtc)
            .OrderByDescending(m => m.Fecha).ThenByDescending(m => m.CreatedAt)
            .Select(m => (decimal?)m.SaldoResultante)
            .FirstOrDefaultAsync(cancellationToken) ?? 0m;

    /// <inheritdoc />
    public async Task<PagedResult<MovimientoLectura>> ListarAsync(
        Guid insumoId, DateTime desdeUtc, DateTime hastaUtc, int page, int pageSize, CancellationToken cancellationToken)
    {
        var consulta = _contexto.MovimientosInventario
            .AsNoTracking()
            .Where(m => m.InsumoId == insumoId && m.Fecha >= desdeUtc && m.Fecha < hastaUtc);

        var total = await consulta.CountAsync(cancellationToken);
        var items = await consulta
            .OrderBy(m => m.Fecha).ThenBy(m => m.CreatedAt).ThenBy(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        var usuarioIds = items.Where(m => m.UsuarioId != null).Select(m => m.UsuarioId!.Value).Distinct().ToList();
        var nombres = await _contexto.Users
            .Where(u => usuarioIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.NombreCompleto, cancellationToken);

        var lecturas = items
            .Select(m => new MovimientoLectura(m, m.UsuarioId is { } id ? nombres.GetValueOrDefault(id) : null))
            .ToList();
        return new PagedResult<MovimientoLectura>(lecturas, total, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyDictionary<TipoMovimientoInventario, decimal>> TotalesPorTipoAsync(
        Guid insumoId, DateTime desdeUtc, DateTime hastaUtc, CancellationToken cancellationToken) =>
        await _contexto.MovimientosInventario
            .Where(m => m.InsumoId == insumoId && m.Fecha >= desdeUtc && m.Fecha < hastaUtc)
            .GroupBy(m => m.Tipo)
            .Select(g => new { Tipo = g.Key, Total = g.Sum(m => m.Cantidad) })
            .ToDictionaryAsync(x => x.Tipo, x => x.Total, cancellationToken);

    /// <inheritdoc />
    public Task GuardarCambiosAsync(CancellationToken cancellationToken) => GuardadoInventario.GuardarAsync(_contexto, cancellationToken);
}
