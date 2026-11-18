using GIRA.Application.Common;
using GIRA.Application.Common.Inventario;
using GIRA.Application.DTOs.Inventario;
using GIRA.Application.Interfaces.Repository.Inventario;
using GIRA.Domain.Entities.Inventario;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Inventario;

/// <summary>Insumos y sus lotes de perecederos.</summary>
public class InsumoRepository : IInsumoRepository
{
    private readonly GiraDbContext _contexto;

    /// <summary>Crea el repositorio.</summary>
    public InsumoRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    private IQueryable<Insumo> ConCatalogos() =>
        _contexto.Insumos.Include(i => i.CategoriaInsumo).Include(i => i.UnidadMedida).Include(i => i.Proveedor);

    /// <inheritdoc />
    public Task<Insumo?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        ConCatalogos().FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Insumo>> BuscarPorIdsAsync(IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var lista = ids.Distinct().ToList();
        return await ConCatalogos().Where(i => lista.Contains(i.Id)).ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Insumo>> ListarActivosAsync(CancellationToken cancellationToken) =>
        await _contexto.Insumos.Include(i => i.UnidadMedida).Where(i => i.Activo).OrderBy(i => i.Nombre).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<PagedResult<Insumo>> ListarAsync(
        string? buscar, Guid? categoriaId, string? nivel, bool incluirInactivos, int page, int pageSize, CancellationToken cancellationToken)
    {
        var consulta = ConCatalogos().AsNoTracking();
        if (!incluirInactivos)
        {
            consulta = consulta.Where(i => i.Activo);
        }

        if (buscar is not null)
        {
            var texto = buscar.ToLower();
            consulta = consulta.Where(i => i.Nombre.ToLower().Contains(texto));
        }

        if (categoriaId is { } categoria)
        {
            consulta = consulta.Where(i => i.CategoriaInsumoId == categoria);
        }

        // Mismo corte que ReglasInventario.Nivel: crítico hasta la mitad del punto de reorden, bajo hasta el punto.
        consulta = nivel switch
        {
            ReglasInventario.NivelCritico => consulta.Where(i => i.CantidadStock <= i.PuntoReorden / 2),
            ReglasInventario.NivelBajo => consulta.Where(i => i.CantidadStock > i.PuntoReorden / 2 && i.CantidadStock <= i.PuntoReorden),
            ReglasInventario.NivelNormal => consulta.Where(i => i.CantidadStock > i.PuntoReorden),
            _ => consulta
        };

        var total = await consulta.CountAsync(cancellationToken);
        var items = await consulta
            .OrderBy(i => i.Nombre).ThenBy(i => i.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Insumo>(items, total, page, pageSize);
    }

    /// <inheritdoc />
    public async Task<ResumenInsumosDto> ResumenAsync(CancellationToken cancellationToken)
    {
        var activos = _contexto.Insumos.Where(i => i.Activo);
        return new ResumenInsumosDto(
            await activos.CountAsync(cancellationToken),
            await activos.CountAsync(i => i.CantidadStock > i.PuntoReorden / 2 && i.CantidadStock <= i.PuntoReorden, cancellationToken),
            await activos.CountAsync(i => i.CantidadStock <= i.PuntoReorden / 2, cancellationToken));
    }

    /// <inheritdoc />
    public Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken)
    {
        var n = nombre.Trim().ToLower();
        return _contexto.Insumos.AnyAsync(i => i.Nombre.ToLower() == n && (excluirId == null || i.Id != excluirId), cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<string>> PlatosActivosQueLoUsanAsync(Guid insumoId, CancellationToken cancellationToken) =>
        await _contexto.Platos
            .Where(p => p.Activo && p.Receta != null && p.Receta.Detalles.Any(d => d.InsumoId == insumoId))
            .OrderBy(p => p.Nombre)
            .Select(p => p.Nombre)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public void Agregar(Insumo insumo) => _contexto.Insumos.Add(insumo);

    /// <inheritdoc />
    public async Task<IReadOnlyList<LoteInsumo>> ListarLotesConSaldoAsync(Guid insumoId, CancellationToken cancellationToken) =>
        await _contexto.LotesInsumo
            .Where(l => l.InsumoId == insumoId && l.Cantidad > 0)
            .OrderBy(l => l.FechaVencimiento).ThenBy(l => l.FechaEntrada)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<LotePorVencer>> ListarLotesPorVencerAsync(DateOnly hasta, CancellationToken cancellationToken) =>
        await (from l in _contexto.LotesInsumo
               join i in _contexto.Insumos on l.InsumoId equals i.Id
               where i.Activo && l.Cantidad > 0 && l.FechaVencimiento <= hasta
               orderby l.FechaVencimiento
               select new LotePorVencer(l.InsumoId, l.Cantidad, l.FechaVencimiento, i.UnidadMedida!.Abreviatura, i.CantidadStock))
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public void AgregarLote(LoteInsumo lote) => _contexto.LotesInsumo.Add(lote);

    /// <inheritdoc />
    public Task GuardarCambiosAsync(CancellationToken cancellationToken) => GuardadoInventario.GuardarAsync(_contexto, cancellationToken);
}
