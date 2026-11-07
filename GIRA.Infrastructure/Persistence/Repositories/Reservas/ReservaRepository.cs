using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Reservas;

/// <summary>Implementación de <see cref="IReservaRepository"/> contra <see cref="GiraDbContext"/>.</summary>
public class ReservaRepository : IReservaRepository
{
    private readonly GiraDbContext _contexto;

    /// <summary>Crea el repositorio con el contexto de EF Core.</summary>
    public ReservaRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Reserva>> ListarVigentesPorFechaAsync(DateOnly fecha, CancellationToken cancellationToken)
    {
        return await _contexto.Reservas
            .Where(r => r.Fecha == fecha
                && (r.Estado == EstadoReserva.Confirmada || r.Estado == EstadoReserva.EnCurso))
            .ToListAsync(cancellationToken);
    }
}
