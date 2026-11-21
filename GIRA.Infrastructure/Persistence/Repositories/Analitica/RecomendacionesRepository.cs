using GIRA.Application.DTOs.Analitica;
using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Application.Services.Analitica;
using GIRA.Domain.Entities.Analitica;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Analitica;

public sealed class RecomendacionesRepository(GiraDbContext contexto) : IRecomendacionesRepository
{
    public async Task<IReadOnlyList<AlertaRecurrente>> ContarAlertasAsync(
        DateTime desdeUtc, DateTime hastaUtc, CancellationToken ct) =>
        await (from alerta in contexto.AlertasInventario.AsNoTracking()
               join insumo in contexto.Insumos.IgnoreQueryFilters().AsNoTracking() on alerta.InsumoId equals insumo.Id
               where alerta.FechaGeneracion >= desdeUtc && alerta.FechaGeneracion < hastaUtc
               group alerta by new { insumo.Id, insumo.Nombre } into grupo
               select new AlertaRecurrente(grupo.Key.Id, grupo.Key.Nombre, grupo.Count()))
            .ToListAsync(ct);

    public async Task<IReadOnlyList<RecomendacionDto>> GuardarVigentesAsync(
        IReadOnlyList<RecomendacionCalculada> candidatas, DateTimeOffset ahora, CancellationToken ct)
    {
        var claves = candidatas.Select(c => c.Clave).ToArray();
        var existentes = await contexto.Set<Recomendacion>()
            .Where(r => r.Vigente || claves.Contains(r.Clave)).ToListAsync(ct);
        var porClave = existentes.ToDictionary(r => r.Clave);
        var nuevas = new List<RecomendacionDto>();
        foreach (var existente in existentes) existente.Vigente = false;
        foreach (var candidata in candidatas)
        {
            if (!porClave.TryGetValue(candidata.Clave, out var recomendacion))
            {
                recomendacion = new Recomendacion
                {
                    Id = Guid.NewGuid(), Clave = candidata.Clave, Estado = "Nueva", FechaGeneracion = ahora
                };
                contexto.Set<Recomendacion>().Add(recomendacion);
                nuevas.Add(new RecomendacionDto(recomendacion.Id, candidata.Fecha, candidata.Tipo,
                    candidata.Mensaje, candidata.Prioridad, recomendacion.Estado, candidata.Origen, ahora));
            }
            recomendacion.Fecha = candidata.Fecha;
            recomendacion.Tipo = candidata.Tipo;
            recomendacion.Mensaje = candidata.Mensaje;
            recomendacion.Prioridad = candidata.Prioridad;
            recomendacion.Origen = candidata.Origen;
            recomendacion.Vigente = true;
        }
        await contexto.SaveChangesAsync(ct);
        return nuevas;
    }

    public async Task<IReadOnlyList<RecomendacionDto>> ConsultarAsync(string? estado, CancellationToken ct) =>
        await contexto.Set<Recomendacion>().AsNoTracking()
            .Where(r => r.Vigente && (estado == null || r.Estado == estado))
            .OrderBy(r => r.Fecha).ThenBy(r => r.Clave)
            .Select(r => new RecomendacionDto(r.Id, r.Fecha, r.Tipo, r.Mensaje,
                r.Prioridad, r.Estado, r.Origen, r.FechaGeneracion)).ToListAsync(ct);

    public async Task<bool> CambiarEstadoAsync(Guid id, string estado, CancellationToken ct)
    {
        var recomendacion = await contexto.Set<Recomendacion>().SingleOrDefaultAsync(r => r.Id == id && r.Vigente, ct);
        if (recomendacion is null) return false;
        recomendacion.Estado = estado;
        await contexto.SaveChangesAsync(ct);
        return true;
    }
}
