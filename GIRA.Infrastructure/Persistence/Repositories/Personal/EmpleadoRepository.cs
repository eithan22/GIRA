using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Domain.Entities.Personal;
using GIRA.Domain.Exceptions.Personal;
using GIRA.Infrastructure.Persistence.Configurations.Personal;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence.Repositories.Personal;

/// <summary>Implementación de <see cref="IEmpleadoRepository"/> contra <see cref="GiraDbContext"/>.</summary>
public class EmpleadoRepository : IEmpleadoRepository
{
    private readonly GiraDbContext _contexto;

    /// <summary>Crea el repositorio con el contexto de EF Core.</summary>
    public EmpleadoRepository(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<Empleado>> ListarAsync(bool soloActivos, CancellationToken cancellationToken)
    {
        var consulta = _contexto.Empleados.AsNoTracking();
        if (soloActivos)
        {
            consulta = consulta.Where(e => e.Activo);
        }

        return await consulta
            .OrderBy(e => e.Apellido)
            .ThenBy(e => e.Nombre)
            .ToListAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Empleado?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
        await _contexto.Empleados.FirstOrDefaultAsync(e => e.Id == id, cancellationToken);

    /// <inheritdoc />
    public async Task<bool> ExisteCedulaAsync(string cedula, Guid? excluirId, CancellationToken cancellationToken)
    {
        var consulta = _contexto.Empleados.AsNoTracking().Where(e => e.Cedula == cedula);
        if (excluirId.HasValue)
        {
            consulta = consulta.Where(e => e.Id != excluirId.Value);
        }

        return await consulta.AnyAsync(cancellationToken);
    }

    /// <inheritdoc />
    public void Agregar(Empleado empleado) => _contexto.Empleados.Add(empleado);

    /// <inheritdoc />
    public async Task GuardarCambiosAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _contexto.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (ViolacionesIndiceUnico.Es(ex, EmpleadoConfiguration.IndiceCedula))
        {
            throw new EmpleadoCedulaDuplicadaException("Otra solicitud acaba de registrar esa cédula.", ex);
        }
    }
}
