using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Infrastructure.Persistence.Context;

namespace GIRA.Infrastructure.Persistence.Repositories.Reservas;

/// <summary>Implementación de <see cref="IVisitaRepository"/> contra <see cref="GiraDbContext"/>.</summary>
public class VisitaRepository : IVisitaRepository
{
    private readonly GiraDbContext _contexto;

    /// <summary>Crea el repositorio con el contexto de EF Core.</summary>
    public VisitaRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <inheritdoc />
    public void Agregar(VisitaSinReserva visita)
    {
        _contexto.VisitasSinReserva.Add(visita);
    }

    /// <inheritdoc />
    public Task GuardarCambiosAsync(CancellationToken cancellationToken)
    {
        return _contexto.SaveChangesAsync(cancellationToken);
    }
}
