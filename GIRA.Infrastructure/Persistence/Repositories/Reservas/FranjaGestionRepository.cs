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

public class FranjaGestionRepository : IFranjaGestionRepository
{
    private readonly GiraDbContext _contexto;

    public FranjaGestionRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    public Task<FranjaHoraria?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return _contexto.FranjasHorarias.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);
    }

    public Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken)
    {
        var normalizado = nombre.Trim().ToLower();

        return _contexto.FranjasHorarias.AnyAsync(
            f => f.Nombre.ToLower() == normalizado && (excluirId == null || f.Id != excluirId),
            cancellationToken);
    }

    public Task<bool> ExisteSolapamientoAsync(
        TimeOnly inicio, TimeOnly fin, Guid? excluirId, CancellationToken cancellationToken)
    {
        // [a, b) y [c, d) se solapan si a < d y c < b; si uno termina justo cuando el otro empieza, no.
        return _contexto.FranjasHorarias.AnyAsync(
            f => f.Activa
                && (excluirId == null || f.Id != excluirId)
                && f.HoraInicio < fin
                && inicio < f.HoraFin,
            cancellationToken);
    }

    public async Task<PagedResult<FranjaHoraria>> ListarAsync(
        bool? activa, int page, int pageSize, CancellationToken cancellationToken)
    {
        IQueryable<FranjaHoraria> consulta = _contexto.FranjasHorarias.AsNoTracking();
        if (activa.HasValue)
        {
            var valor = activa.Value;
            consulta = consulta.Where(f => f.Activa == valor);
        }

        var total = await consulta.CountAsync(cancellationToken);
        var items = await consulta
            .OrderBy(f => f.HoraInicio).ThenBy(f => f.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new PagedResult<FranjaHoraria>(items, total, page, pageSize);
    }

    public void Agregar(FranjaHoraria franja)
    {
        _contexto.FranjasHorarias.Add(franja);
    }

    public async Task GuardarCambiosAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _contexto.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new SalonConflictoConcurrenciaException("La franja cambió mientras se guardaba.", ex);
        }
        catch (DbUpdateException ex) when (ViolacionesIndiceUnico.Es(ex, FranjaHorariaConfiguration.IndiceNombre))
        {
            throw new SalonDuplicadoException("Ya existe una franja con ese nombre.", ex);
        }
    }
}