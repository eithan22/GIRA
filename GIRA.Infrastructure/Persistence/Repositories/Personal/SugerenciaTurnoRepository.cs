using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Domain.Entities.Personal;
using GIRA.Domain.Enums.Personal;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Personal;

/// <summary>Implementación de <see cref="ISugerenciaTurnoRepository"/> contra <see cref="GiraDbContext"/>.</summary>
public class SugerenciaTurnoRepository : ISugerenciaTurnoRepository
{
    private readonly GiraDbContext _contexto;

    /// <summary>Crea el repositorio con el contexto de EF Core.</summary>
    public SugerenciaTurnoRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <inheritdoc />
    public async Task<SugerenciaTurno?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
        await _contexto.Sugerencias.FirstOrDefaultAsync(s => s.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<SugerenciaTurno>> ListarAsync(EstadoSugerencia? estado, CancellationToken cancellationToken)
    {
        IQueryable<SugerenciaTurno> consulta = _contexto.Sugerencias.AsNoTracking();
        if (estado is not null)
        {
            consulta = consulta.Where(s => s.Estado == estado.Value);
        }

        return await consulta
            .OrderBy(s => s.Fecha)
            .ThenBy(s => s.FranjaHorariaId)
            .ThenBy(s => s.CargoId)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> ExisteEventoAsync(Guid eventoId, CancellationToken cancellationToken) =>
        await _contexto.Sugerencias.AnyAsync(s => s.EventoId == eventoId, cancellationToken);

    /// <inheritdoc />
    public async Task<IReadOnlyList<SugerenciaTurno>> ListarPendientesEntreAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken) =>
        await _contexto.Sugerencias
            .Where(s => s.Estado == EstadoSugerencia.Pendiente && s.Fecha >= desde && s.Fecha <= hasta)
            .ToListAsync(cancellationToken);

    /// <inheritdoc />
    public void Agregar(SugerenciaTurno sugerencia) => _contexto.Sugerencias.Add(sugerencia);

    /// <inheritdoc />
    public async Task GuardarCambiosAsync(CancellationToken cancellationToken) =>
        await _contexto.SaveChangesAsync(cancellationToken);
}
