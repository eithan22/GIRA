using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Domain.Entities.Personal;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Personal;

/// <summary>Implementación de <see cref="IDisponibilidadRepository"/> contra <see cref="GiraDbContext"/>.</summary>
public class DisponibilidadRepository : IDisponibilidadRepository
{
    private readonly GiraDbContext _contexto;

    /// <summary>Crea el repositorio con el contexto de EF Core.</summary>
    public DisponibilidadRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Disponibilidad>> ListarPorEmpleadoAsync(Guid empleadoId, CancellationToken cancellationToken) =>
        await _contexto.Disponibilidades
            .AsNoTracking()
            .Where(d => d.EmpleadoId == empleadoId)
            .OrderBy(d => d.DiaSemana)
            .ThenBy(d => d.HoraInicio)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<Disponibilidad?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
        await _contexto.Disponibilidades.FirstOrDefaultAsync(d => d.Id == id, cancellationToken);

    /// <inheritdoc />
    public void Agregar(Disponibilidad disponibilidad) => _contexto.Disponibilidades.Add(disponibilidad);

    /// <inheritdoc />
    public void Eliminar(Disponibilidad disponibilidad) => _contexto.Disponibilidades.Remove(disponibilidad);

    /// <inheritdoc />
    public async Task GuardarCambiosAsync(CancellationToken cancellationToken) =>
        await _contexto.SaveChangesAsync(cancellationToken);
}
