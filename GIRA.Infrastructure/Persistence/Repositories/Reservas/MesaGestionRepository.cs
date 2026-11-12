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

public class MesaGestionRepository : IMesaGestionRepository
{
    private readonly GiraDbContext _contexto;

    public MesaGestionRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    public Task<Mesa?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _contexto.Mesas.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);
    }

    public Task<bool> ExisteNumeroAsync(int numero, Guid? excluirId, CancellationToken cancellationToken)
    {
        return _contexto.Mesas.AnyAsync(
            m => m.Numero == numero && (excluirId == null || m.Id != excluirId),
            cancellationToken);
    }

    public async Task<PagedResult<Mesa>> ListarAsync(
        Guid? zonaId, bool? activa, int page, int pageSize, CancellationToken cancellationToken)
    {
        IQueryable<Mesa> consulta = _contexto.Mesas.AsNoTracking();
        if (zonaId.HasValue)
        {
            var zona = zonaId.Value;
            consulta = consulta.Where(m => m.ZonaId == zona);
        }

        if (activa.HasValue)
        {
            var valor = activa.Value;
            consulta = consulta.Where(m => m.Activa == valor);
        }

        var total = await consulta.CountAsync(cancellationToken);
        var items = await consulta
            .OrderBy(m => m.Numero).ThenBy(m => m.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<Mesa>(items, total, page, pageSize);
    }

    public void Agregar(Mesa mesa)
    {
        _contexto.Mesas.Add(mesa);
    }

    public async Task GuardarCambiosAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _contexto.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new SalonConflictoConcurrenciaException("La mesa cambió mientras se guardaba.", ex);
        }
        catch (DbUpdateException ex) when (ViolacionesIndiceUnico.Es(ex, MesaConfiguration.IndiceNumero))
        {
            throw new SalonDuplicadoException("Ya existe una mesa con ese número.", ex);
        }
    }
}