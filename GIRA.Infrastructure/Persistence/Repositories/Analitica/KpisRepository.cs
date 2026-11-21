using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Application.Common;
using GIRA.Domain.Entities.Analitica;
using GIRA.Domain.Entities.Personal;
using GIRA.Domain.Enums.Inventario;
using GIRA.Domain.Enums.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Analitica;

/// <summary>Lecturas de Reservas e Inventario para Analítica.</summary>
public sealed class KpisRepository(GiraDbContext contexto) : IKpisRepository
{
    public async Task<(int Creadas, int Canceladas)> ContarReservasAsync(
        DateTime desdeUtc, DateTime hastaUtcExclusivo, CancellationToken cancellationToken)
    {
        var resumen = await contexto.Reservas.AsNoTracking()
            .Where(r => r.CreatedAt >= desdeUtc && r.CreatedAt < hastaUtcExclusivo)
            .GroupBy(_ => 1)
            .Select(g => new { Creadas = g.Count(), Canceladas = g.Count(r => r.Estado == EstadoReserva.Cancelada) })
            .SingleOrDefaultAsync(cancellationToken);
        return resumen is null ? (0, 0) : (resumen.Creadas, resumen.Canceladas);
    }

    public Task<int> ContarAlertasAbiertasAsync(CancellationToken cancellationToken) =>
        contexto.AlertasInventario.AsNoTracking()
            .CountAsync(a => a.Estado == EstadoAlertaInventario.Abierta, cancellationToken);

    public async Task<decimal?> CalcularPrecisionModeloAsync(
        DateOnly desde, DateOnly hasta, CancellationToken cancellationToken)
    {
        var observaciones = await contexto.Set<RegistroAfluencia>().AsNoTracking()
            .Where(r => r.Tipo == TipoRegistroAfluencia.VisitaCompletada &&
                        r.Fecha >= desde && r.Fecha <= hasta)
            .GroupBy(r => new { r.Fecha, r.FranjaHorariaId })
            .Select(g => new { g.Key.Fecha, g.Key.FranjaHorariaId, Real = g.Sum(r => r.Personas) })
            .ToListAsync(cancellationToken);
        if (observaciones.Count == 0)
            return null;

        var predicciones = await contexto.Set<PrediccionAfluencia>().AsNoTracking()
            .Where(p => p.Fecha >= desde && p.Fecha <= hasta)
            .Select(p => new
            {
                p.Fecha,
                p.FranjaHorariaId,
                p.AfluenciaEstimada,
                p.GeneradaEn
            })
            .ToListAsync(cancellationToken);

        var errores = new List<decimal>();
        foreach (var observacion in observaciones)
        {
            var inicioServicioUtc = new DateTimeOffset(TimeZoneInfo.ConvertTimeToUtc(
                observacion.Fecha.ToDateTime(TimeOnly.MinValue), HoraLocalGira.ZonaHoraria), TimeSpan.Zero);
            var estimacion = predicciones
                .Where(p => p.Fecha == observacion.Fecha &&
                            p.FranjaHorariaId == observacion.FranjaHorariaId &&
                            p.GeneradaEn < inicioServicioUtc)
                .OrderByDescending(p => p.GeneradaEn)
                .Select(p => (int?)p.AfluenciaEstimada)
                .FirstOrDefault();
            if (estimacion.HasValue)
                errores.Add(Math.Abs(estimacion.Value - observacion.Real));
        }

        return errores.Count == 0 ? null : errores.Average();
    }

    public async Task<(int DiasRegistrados, int CapacidadTotal)> ConsultarCapacidadAsync(
        DateOnly desde, DateOnly hasta, CancellationToken cancellationToken)
    {
        var capacidades = await contexto.Set<CapacidadOperativaDiaria>().AsNoTracking()
            .Where(c => c.Fecha >= desde && c.Fecha <= hasta)
            .Select(c => c.CapacidadTotal)
            .ToListAsync(cancellationToken);
        return (capacidades.Count, capacidades.Sum());
    }

    public async Task<(decimal? CostoSalidas, decimal? CostoMermas)> ConsultarCostosAsync(
        DateTime desdeUtc, DateTime hastaUtcExclusivo, CancellationToken cancellationToken)
    {
        var movimientos = await contexto.MovimientosInventario.AsNoTracking()
            .Where(m => m.Fecha >= desdeUtc && m.Fecha < hastaUtcExclusivo &&
                        (m.Tipo == TipoMovimientoInventario.Salida || m.Tipo == TipoMovimientoInventario.Merma))
            .Select(m => new { m.Tipo, m.Cantidad, m.CostoUnitario })
            .ToListAsync(cancellationToken);
        if (movimientos.Count == 0 || movimientos.Any(m => m.CostoUnitario is null))
            return (null, null);

        var costoSalidas = movimientos
            .Where(m => m.Tipo == TipoMovimientoInventario.Salida)
            .Sum(m => Math.Abs(m.Cantidad) * m.CostoUnitario!.Value);
        var costoMermas = movimientos
            .Where(m => m.Tipo == TipoMovimientoInventario.Merma)
            .Sum(m => Math.Abs(m.Cantidad) * m.CostoUnitario!.Value);
        return (costoSalidas, costoMermas);
    }

    public async Task<decimal?> ConsultarHorasPersonalAsync(
        DateTime desdeUtc, DateTime hastaUtcExclusivo, CancellationToken cancellationToken)
    {
        var turnos = await contexto.Set<MarcacionTurno>().AsNoTracking()
            .Where(m => m.InicioEn >= desdeUtc && m.InicioEn < hastaUtcExclusivo && m.FinEn != null)
            .Select(m => new { m.InicioEn, m.FinEn, m.MinutosPausa })
            .ToListAsync(cancellationToken);
        if (turnos.Count == 0)
            return null;

        return turnos.Sum(m => (decimal)((m.FinEn!.Value - m.InicioEn).TotalMinutes - m.MinutosPausa)) / 60m;
    }

    public async Task<IReadOnlyDictionary<Guid, string>> ObtenerFranjasAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken) =>
        await contexto.FranjasHorarias.IgnoreQueryFilters().AsNoTracking()
            .Where(f => ids.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, f => f.Nombre, cancellationToken);
}
