using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Domain.Entities.Analitica;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Analitica;

public sealed class CapacidadOperativaRepository(GiraDbContext contexto) : ICapacidadOperativaRepository
{
    public Task<bool> ExisteAsync(DateOnly fecha, CancellationToken cancellationToken) =>
        contexto.Set<CapacidadOperativaDiaria>().AnyAsync(c => c.Fecha == fecha, cancellationToken);

    public async Task<int> CalcularCapacidadActualAsync(CancellationToken cancellationToken)
    {
        var capacidadMesas = await contexto.Mesas.AsNoTracking()
            .Where(m => m.Activa && contexto.Zonas.Any(z => z.Id == m.ZonaId && z.Activa))
            .SumAsync(m => (int?)m.Capacidad, cancellationToken) ?? 0;
        var franjasActivas = await contexto.FranjasHorarias.AsNoTracking()
            .CountAsync(f => f.Activa, cancellationToken);
        return checked(capacidadMesas * franjasActivas);
    }

    public async Task GuardarAsync(CapacidadOperativaDiaria capacidad, CancellationToken cancellationToken)
    {
        contexto.Set<CapacidadOperativaDiaria>().Add(capacidad);
        await contexto.SaveChangesAsync(cancellationToken);
    }
}
