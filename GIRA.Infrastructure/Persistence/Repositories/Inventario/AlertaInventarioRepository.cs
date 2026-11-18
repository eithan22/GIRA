using GIRA.Application.Common;
using GIRA.Application.Interfaces.Repository.Inventario;
using GIRA.Domain.Entities.Inventario;
using GIRA.Domain.Enums.Inventario;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Inventario;

/// <summary>Alertas de inventario.</summary>
public class AlertaInventarioRepository : IAlertaInventarioRepository
{
    private readonly GiraDbContext _contexto;

    /// <summary>Crea el repositorio.</summary>
    public AlertaInventarioRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AlertaInventario>> ListarAbiertasAsync(IReadOnlyCollection<Guid> insumoIds, CancellationToken cancellationToken)
    {
        var lista = insumoIds.Distinct().ToList();
        return await _contexto.AlertasInventario
            .Where(a => a.Estado == EstadoAlertaInventario.Abierta && lista.Contains(a.InsumoId))
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<PagedResult<AlertaLectura>> ListarAsync(EstadoAlertaInventario? estado, int page, int pageSize, CancellationToken cancellationToken)
    {
        var consulta = _contexto.AlertasInventario.AsNoTracking();
        if (estado is { } valor)
        {
            consulta = consulta.Where(a => a.Estado == valor);
        }

        var total = await consulta.CountAsync(cancellationToken);
        var items = await consulta
            .Include(a => a.Insumo).ThenInclude(i => i!.UnidadMedida)
            .OrderByDescending(a => a.FechaGeneracion).ThenBy(a => a.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<AlertaLectura>(await ConNombresAsync(items, cancellationToken), total, page, pageSize);
    }

    /// <inheritdoc />
    public Task<AlertaInventario?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        _contexto.AlertasInventario.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<AlertaLectura?> ObtenerLecturaAsync(Guid id, CancellationToken cancellationToken)
    {
        var alerta = await _contexto.AlertasInventario
            .AsNoTracking()
            .Include(a => a.Insumo).ThenInclude(i => i!.UnidadMedida)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
        return alerta is null ? null : (await ConNombresAsync([alerta], cancellationToken))[0];
    }

    /// <inheritdoc />
    public void Agregar(AlertaInventario alerta) => _contexto.AlertasInventario.Add(alerta);

    /// <inheritdoc />
    public Task GuardarCambiosAsync(CancellationToken cancellationToken) => GuardadoInventario.GuardarAsync(_contexto, cancellationToken);

    private async Task<IReadOnlyList<AlertaLectura>> ConNombresAsync(IReadOnlyList<AlertaInventario> alertas, CancellationToken cancellationToken)
    {
        var usuarioIds = alertas.Where(a => a.AtendidaPor != null).Select(a => a.AtendidaPor!.Value).Distinct().ToList();
        var nombres = await _contexto.Users
            .Where(u => usuarioIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.NombreCompleto, cancellationToken);
        return alertas
            .Select(a => new AlertaLectura(a, a.AtendidaPor is { } id ? nombres.GetValueOrDefault(id) : null))
            .ToList();
    }
}
