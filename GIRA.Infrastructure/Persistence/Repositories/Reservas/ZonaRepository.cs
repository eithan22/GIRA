using GIRA.Application.Common;
using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Exceptions.Reservas;
using GIRA.Infrastructure.Persistence.Configurations.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Text;

namespace GIRA.Infrastructure.Persistence.Repositories.Reservas;

public class ZonaRepository : IZonaRepository
{
    private readonly GiraDbContext _contexto;

    public ZonaRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    public Task<Zona?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _contexto.Zonas.FirstOrDefaultAsync(z => z.Id == id, cancellationToken);
    }

    public Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken)
    {
        var normalizado = nombre.Trim().ToLower();

        return _contexto.Zonas.AnyAsync(
            z => z.Nombre.ToLower() == normalizado && (excluirId == null || z.Id != excluirId),
            cancellationToken);
    }

    public async Task<PagedResult<Zona>> ListarAsync(bool? activa, int page, int pageSize, CancellationToken cancellationToken)
    {
        IQueryable<Zona> consulta = _contexto.Zonas.AsNoTracking();
        if (activa.HasValue)
        {
            var valor = activa.Value;
            consulta = consulta.Where(z => z.Activa == valor);
        }

        var total = await consulta.CountAsync(cancellationToken);
        var items = await consulta
            .OrderBy(z => z.Nombre).ThenBy(z => z.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Zona>(items, total, page, pageSize);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> ObtenerNombresAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var lista = ids.ToList();

        return await _contexto.Zonas
            .AsNoTracking()
            .Where(z => lista.Contains(z.Id))
            .ToDictionaryAsync(z => z.Id, z => z.Nombre, cancellationToken);
    }

    public void Agregar(Zona zona)
    {
        _contexto.Zonas.Add(zona);
    }

    public async Task GuardarCambiosAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _contexto.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new SalonConflictoConcurrenciaException("La zona cambió mientras se guardaba.", ex);
        }
        catch (DbUpdateException ex) when (ViolacionesIndiceUnico.Es(ex, ZonaConfiguration.IndiceNombre))
        {
            throw new SalonDuplicadoException("Ya existe una zona con ese nombre.", ex);
        }
    }
}