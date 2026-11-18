using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Domain.Entities.Personal;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Personal;

/// <summary>Implementación de <see cref="IRequerimientoPersonalRepository"/> contra <see cref="GiraDbContext"/>.</summary>
public class RequerimientoPersonalRepository : IRequerimientoPersonalRepository
{
    private readonly GiraDbContext _contexto;

    /// <summary>Crea el repositorio con el contexto de EF Core.</summary>
    public RequerimientoPersonalRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<RequerimientoPersonal>> ListarAsync(CancellationToken cancellationToken) =>
        await _contexto.RequerimientosPersonal.AsNoTracking().OrderBy(r => r.CargoId).ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<RequerimientoPersonal?> BuscarPorCargoAsync(Guid cargoId, CancellationToken cancellationToken) =>
        await _contexto.RequerimientosPersonal.FirstOrDefaultAsync(r => r.CargoId == cargoId, cancellationToken);

    /// <inheritdoc />
    public void Agregar(RequerimientoPersonal requerimiento) => _contexto.RequerimientosPersonal.Add(requerimiento);

    /// <inheritdoc />
    public async Task GuardarCambiosAsync(CancellationToken cancellationToken) =>
        await _contexto.SaveChangesAsync(cancellationToken);
}
