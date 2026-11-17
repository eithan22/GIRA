using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Reservas;

/// <summary>Implementación de <see cref="IMesaRepository"/> contra <see cref="GiraDbContext"/>.</summary>
public class MesaRepository : IMesaRepository
{
    private readonly GiraDbContext _contexto;

    /// <summary>Crea el repositorio con el contexto de EF Core.</summary>
    public MesaRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Mesa>> ListarActivasAsync(CancellationToken cancellationToken)
    {
        return await _contexto.Mesas
            .Where(m => m.Activa)
            .Join(_contexto.Zonas.Where(z => z.Activa), m => m.ZonaId, z => z.Id, (m, _) => m)
            .ToListAsync(cancellationToken);
    }
}
