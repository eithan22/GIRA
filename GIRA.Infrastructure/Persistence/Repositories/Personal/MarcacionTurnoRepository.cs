using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Domain.Entities.Personal;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Personal;

public sealed class MarcacionTurnoRepository(GiraDbContext contexto) : IMarcacionTurnoRepository
{
    public Task<MarcacionTurno?> ObtenerAbiertaAsync(Guid usuarioId, CancellationToken cancellationToken) =>
        contexto.Set<MarcacionTurno>().SingleOrDefaultAsync(
            m => m.UsuarioId == usuarioId && m.FinEn == null, cancellationToken);

    public async Task AgregarAsync(MarcacionTurno marcacion, CancellationToken cancellationToken) =>
        await contexto.Set<MarcacionTurno>().AddAsync(marcacion, cancellationToken);

    public Task GuardarAsync(CancellationToken cancellationToken) =>
        contexto.SaveChangesAsync(cancellationToken);
}
