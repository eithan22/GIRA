using GIRA.Application.Events.Analitica;
using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Application.Services.Analitica;
using GIRA.Domain.Entities.Analitica;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace GIRA.Infrastructure.Persistence.Repositories.Analitica;

public sealed class GeneracionPrediccionRepository(GiraDbContext contexto, IConfiguration configuracion) : IGeneracionPrediccionRepository
{
    public async Task<IReadOnlyList<Guid>> ObtenerFranjasActivasAsync(CancellationToken cancellationToken) =>
        await contexto.FranjasHorarias.AsNoTracking().Where(f => f.Activa)
            .Select(f => f.Id).ToListAsync(cancellationToken);

    public Task<bool> ExisteAsync(DateOnly fechaProceso, CancellationToken cancellationToken) =>
        contexto.Set<GeneracionPrediccion>().AnyAsync(g => g.FechaProceso == fechaProceso, cancellationToken);

    public async Task GuardarAsync(GeneracionPrediccion generacion, IReadOnlyList<PrediccionAfluencia> predicciones,
        IReadOnlyList<PublicacionPrediccion> publicaciones, CancellationToken cancellationToken)
    {
        await using var transaccion = await contexto.Database.BeginTransactionAsync(cancellationToken);
        var desde = generacion.FechaProceso.AddDays(1);
        var hasta = generacion.FechaProceso.AddDays(PrediccionBase.HorizonteDias);
        var reemplazos = predicciones.Select(p => (p.Fecha, p.FranjaHorariaId)).ToHashSet();
        var anteriores = await contexto.Set<PrediccionAfluencia>()
            .Where(p => p.Vigente && p.Fecha >= desde && p.Fecha <= hasta).ToListAsync(cancellationToken);
        foreach (var anterior in anteriores)
        {
            if (reemplazos.Contains((anterior.Fecha, anterior.FranjaHorariaId)))
                anterior.Vigente = false;
        }
        await contexto.SaveChangesAsync(cancellationToken);

        contexto.Set<GeneracionPrediccion>().Add(generacion);
        contexto.Set<PrediccionAfluencia>().AddRange(predicciones);
        contexto.Set<PublicacionPrediccion>().AddRange(publicaciones);
        await contexto.SaveChangesAsync(cancellationToken);
        await transaccion.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<PublicacionPendiente>> ObtenerPendientesAsync(CancellationToken cancellationToken)
    {
        var pendientes = await contexto.Set<PublicacionPrediccion>().AsNoTracking()
            .Where(p => p.PublicadaEn == null).OrderBy(p => p.Fecha).ToListAsync(cancellationToken);
        var resultado = new List<PublicacionPendiente>(pendientes.Count);
        if (pendientes.Count == 0) return resultado;
        var restauranteId = ObtenerRestauranteId(configuracion);
        foreach (var pendiente in pendientes)
        {
            var generadaEn = await contexto.Set<GeneracionPrediccion>().AsNoTracking()
                .Where(g => g.Id == pendiente.GeneracionId)
                .Select(g => g.GeneradaEn)
                .SingleAsync(cancellationToken);
            var franjas = await contexto.Set<PrediccionAfluencia>().AsNoTracking()
                .Where(p => p.GeneracionId == pendiente.GeneracionId && p.Fecha == pendiente.Fecha)
                .Select(p => new AfluenciaEstimadaFranja(
                    p.Fecha, p.FranjaHorariaId, p.AfluenciaEstimada, p.Id, p.VersionModelo))
                .ToListAsync(cancellationToken);
            if (franjas.Count == 0)
                throw new InvalidOperationException($"La publicación de predicción {pendiente.Id} no tiene franjas.");
            if (franjas.Any(f => f.PrediccionId is null || f.PrediccionId == Guid.Empty ||
                string.IsNullOrWhiteSpace(f.VersionModelo)))
                throw new InvalidOperationException($"La publicación de predicción {pendiente.Id} contiene metadatos inválidos.");
            resultado.Add(new PublicacionPendiente(pendiente.Id,
                new PrediccionAfluenciaGeneradaEvent(pendiente.Fecha, pendiente.Fecha, franjas,
                    pendiente.Id, 1, restauranteId, generadaEn.ToUniversalTime(),
                    nameof(PrediccionAfluenciaGeneradaEvent))));
        }

        return resultado;
    }

    public static Guid ObtenerRestauranteId(IConfiguration configuracion)
    {
        if (!Guid.TryParse(configuracion["Analitica:RestauranteId"], out var id) || id == Guid.Empty)
            throw new InvalidOperationException(
                "Analitica:RestauranteId debe contener el GUID real del restaurante para publicar predicciones.");
        return id;
    }

    public async Task MarcarPublicadaAsync(Guid id, DateTimeOffset fecha, CancellationToken cancellationToken)
    {
        var publicacion = await contexto.Set<PublicacionPrediccion>().SingleAsync(p => p.Id == id, cancellationToken);
        publicacion.PublicadaEn = fecha;
        await contexto.SaveChangesAsync(cancellationToken);
    }
}
