using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Domain.Entities.Personal;
using GIRA.Domain.Enums.Personal;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Personal;

/// <summary>Implementación de <see cref="IAusenciaRepository"/> contra <see cref="GiraDbContext"/>.</summary>
public class AusenciaRepository : IAusenciaRepository
{
    private readonly GiraDbContext _contexto;

    /// <summary>Crea el repositorio con el contexto de EF Core.</summary>
    public AusenciaRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Ausencia>> ListarAsync(Guid? empleadoId, CancellationToken cancellationToken)
    {
        var consulta = _contexto.Ausencias.AsNoTracking();
        if (empleadoId.HasValue)
        {
            consulta = consulta.Where(a => a.EmpleadoId == empleadoId.Value);
        }

        return await consulta
            .OrderByDescending(a => a.FechaDesde)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Ausencia?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
        await _contexto.Ausencias.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);

    /// <inheritdoc />
    public void Agregar(Ausencia ausencia) => _contexto.Ausencias.Add(ausencia);

    /// <inheritdoc />
    public async Task GuardarCambiosAsync(CancellationToken cancellationToken) =>
        await _contexto.SaveChangesAsync(cancellationToken);

    /// <inheritdoc />
    public async Task<bool> TieneAprobadaEnFechaAsync(Guid empleadoId, DateOnly fecha, CancellationToken cancellationToken) =>
        await _contexto.Ausencias.AsNoTracking().AnyAsync(a =>
            a.EmpleadoId == empleadoId &&
            a.Estado == EstadoAusencia.Aprobada &&
            a.FechaDesde <= fecha &&
            fecha <= a.FechaHasta,
            cancellationToken);
}
