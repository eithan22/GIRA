using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Domain.Entities.Personal;
using GIRA.Infrastructure.Persistence.Context;

namespace GIRA.Infrastructure.Persistence.Repositories.Personal;

/// <summary>Implementación de <see cref="IEventoPrediccionDescartadoRepository"/> contra <see cref="GiraDbContext"/>.</summary>
public class EventoPrediccionDescartadoRepository : IEventoPrediccionDescartadoRepository
{
    private readonly GiraDbContext _contexto;

    /// <summary>Crea el repositorio con el contexto de EF Core.</summary>
    public EventoPrediccionDescartadoRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <inheritdoc />
    public void Agregar(EventoPrediccionDescartado evento) => _contexto.EventosPrediccionDescartados.Add(evento);

    /// <inheritdoc />
    public async Task GuardarCambiosAsync(CancellationToken cancellationToken) =>
        await _contexto.SaveChangesAsync(cancellationToken);
}
