using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Domain.Entities.Analitica;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Analitica;

/// <summary>Persistencia de historial y predicciones usando el contexto único de GIRA.</summary>
public sealed class AnaliticaRepository(GiraDbContext dbContext) : IAnaliticaRepository
{
    /// <inheritdoc />
    public Task<bool> ExisteReservaAsync(Guid reservaId, CancellationToken cancellationToken) =>
        dbContext.HistorialAfluencia
            .AnyAsync(registro => registro.ReservaId == reservaId, cancellationToken);

    /// <inheritdoc />
    public async Task AgregarHistorialAsync(
        HistorialAfluencia registro,
        CancellationToken cancellationToken)
    {
        registro.CreatedAt = DateTime.UtcNow;
        await dbContext.HistorialAfluencia.AddAsync(registro, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<HistorialAfluencia>> ObtenerHistorialAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken cancellationToken) =>
        await dbContext.HistorialAfluencia
            .AsNoTracking()
            .Where(registro => registro.Fecha >= desde && registro.Fecha <= hasta)
            .OrderBy(registro => registro.Fecha)
            .ThenBy(registro => registro.HoraInicio)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<PrediccionResultado>> ObtenerPrediccionesAsync(
        DateOnly fecha,
        CancellationToken cancellationToken) =>
        await dbContext.PrediccionesAfluencia
            .AsNoTracking()
            .Where(prediccion => prediccion.Fecha == fecha)
            .OrderBy(prediccion => prediccion.HoraInicio)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task ReemplazarPrediccionesAsync(
        DateOnly desde,
        DateOnly hasta,
        IReadOnlyCollection<PrediccionResultado> predicciones,
        CancellationToken cancellationToken)
    {
        var existentes = await dbContext.PrediccionesAfluencia
            .Where(prediccion => prediccion.Fecha >= desde && prediccion.Fecha <= hasta)
            .ToListAsync(cancellationToken);

        var createdAt = DateTime.UtcNow;
        foreach (var prediccion in predicciones)
        {
            prediccion.CreatedAt = createdAt;
        }

        dbContext.PrediccionesAfluencia.RemoveRange(existentes);
        await dbContext.PrediccionesAfluencia.AddRangeAsync(predicciones, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
