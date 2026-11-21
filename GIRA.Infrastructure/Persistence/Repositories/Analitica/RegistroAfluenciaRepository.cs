using GIRA.Application.DTOs.Analitica;
using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Domain.Entities.Analitica;
using GIRA.Infrastructure.Persistence.Configurations.Analitica;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Analitica;

public sealed class RegistroAfluenciaRepository(GiraDbContext contexto) : IRegistroAfluenciaRepository
{
    public async Task<IReadOnlyList<HistorialAfluenciaDto>> ConsultarAsync(
        DateOnly desde, DateOnly hasta, CancellationToken cancellationToken)
    {
        return await contexto.Set<RegistroAfluencia>().AsNoTracking()
            .Where(r => r.Fecha >= desde && r.Fecha <= hasta)
            .GroupBy(r => new { r.Fecha, r.FranjaHorariaId })
            .OrderBy(g => g.Key.Fecha).ThenBy(g => g.Key.FranjaHorariaId)
            .Select(g => new HistorialAfluenciaDto(
                g.Key.Fecha,
                g.Key.FranjaHorariaId,
                g.Sum(r => r.Tipo == TipoRegistroAfluencia.VisitaCompletada &&
                           r.Origen == OrigenRegistroAfluencia.Reserva ? r.Personas : 0),
                g.Sum(r => r.Tipo == TipoRegistroAfluencia.VisitaCompletada &&
                           r.Origen == OrigenRegistroAfluencia.VisitaSinReserva ? r.Personas : 0),
                g.Sum(r => r.Tipo == TipoRegistroAfluencia.VisitaCompletada ? r.Personas : 0),
                g.Count(r => r.Tipo == TipoRegistroAfluencia.VisitaCompletada &&
                             r.Origen == OrigenRegistroAfluencia.Reserva),
                g.Count(r => r.Tipo == TipoRegistroAfluencia.ReservaNoAsistida)))
            .ToListAsync(cancellationToken);
    }

    public async Task RegistrarAsync(RegistroAfluencia registro, CancellationToken cancellationToken)
    {
        var franjaExiste = await contexto.FranjasHorarias.IgnoreQueryFilters()
            .AnyAsync(f => f.Id == registro.FranjaHorariaId, cancellationToken);
        if (!franjaExiste)
        {
            throw new InvalidOperationException(
                $"La franja horaria {registro.FranjaHorariaId} del evento de afluencia no existe.");
        }

        var existente = await BuscarAsync(registro, cancellationToken);
        if (existente is not null)
        {
            VerificarCoincidencia(existente, registro);
            return;
        }

        registro.Id = Guid.NewGuid();
        contexto.Set<RegistroAfluencia>().Add(registro);
        try
        {
            await contexto.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ViolacionesIndiceUnico.Es(ex, RegistroAfluenciaConfiguration.IndiceOrigenReferencia))
        {
            contexto.Entry(registro).State = EntityState.Detached;
            existente = await BuscarAsync(registro, cancellationToken);
            if (existente is null)
            {
                throw;
            }

            VerificarCoincidencia(existente, registro);
        }
    }

    private Task<RegistroAfluencia?> BuscarAsync(RegistroAfluencia registro, CancellationToken cancellationToken) =>
        contexto.Set<RegistroAfluencia>().AsNoTracking()
            .SingleOrDefaultAsync(r => r.Origen == registro.Origen && r.ReferenciaId == registro.ReferenciaId, cancellationToken);

    private static void VerificarCoincidencia(RegistroAfluencia existente, RegistroAfluencia recibido)
    {
        if (existente.Tipo != recibido.Tipo || existente.Fecha != recibido.Fecha ||
            existente.FranjaHorariaId != recibido.FranjaHorariaId || existente.Personas != recibido.Personas)
        {
            throw new InvalidOperationException(
                $"El evento de afluencia para {recibido.Origen} {recibido.ReferenciaId} contradice el registro existente.");
        }
    }
}
