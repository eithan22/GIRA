using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Domain.Entities.Personal;
using GIRA.Domain.Enums.Personal;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Personal;

/// <summary>Implementación de <see cref="ITurnoRepository"/> contra <see cref="GiraDbContext"/>.</summary>
public class TurnoRepository : ITurnoRepository
{
    private readonly GiraDbContext _contexto;

    /// <summary>Crea el repositorio con el contexto de EF Core.</summary>
    public TurnoRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <inheritdoc />
    public async Task<Turno?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
        await _contexto.Turnos.FirstOrDefaultAsync(t => t.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Turno>> ListarPorEmpleadoYFechaAsync(Guid empleadoId, DateOnly fecha, CancellationToken cancellationToken) =>
        await _contexto.Turnos
            .AsNoTracking()
            .Where(t => t.EmpleadoId == empleadoId && t.Fecha == fecha)
            .OrderBy(t => t.HoraInicio)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<Turno>> ListarPorEmpleadoEntreAsync(Guid empleadoId, DateOnly desde, DateOnly hasta, CancellationToken cancellationToken) =>
        await _contexto.Turnos
            .AsNoTracking()
            .Where(t => t.EmpleadoId == empleadoId && t.Fecha >= desde && t.Fecha <= hasta)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public void Agregar(Turno turno) => _contexto.Turnos.Add(turno);

    /// <inheritdoc />
    public async Task GuardarCambiosAsync(CancellationToken cancellationToken) =>
        await _contexto.SaveChangesAsync(cancellationToken);
}
