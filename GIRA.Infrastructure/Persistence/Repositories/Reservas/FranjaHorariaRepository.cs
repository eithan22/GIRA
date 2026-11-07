using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Reservas;

/// <summary>Implementación de <see cref="IFranjaHorariaRepository"/> contra <see cref="GiraDbContext"/>.</summary>
public class FranjaHorariaRepository : IFranjaHorariaRepository
{
    private readonly GiraDbContext _contexto;

    /// <summary>Crea el repositorio con el contexto de EF Core.</summary>
    public FranjaHorariaRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<FranjaHoraria>> ListarActivasAsync(CancellationToken cancellationToken)
    {
        return await _contexto.FranjasHorarias
            .Where(f => f.Activa)
            .ToListAsync(cancellationToken);
    }
}
