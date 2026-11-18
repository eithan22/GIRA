using GIRA.Domain.Entities.Personal;

namespace GIRA.Application.Interfaces.Repository.Personal;

/// <summary>Acceso a las ausencias de los empleados (esquema <c>personal</c>).</summary>
public interface IAusenciaRepository
{
    /// <summary>Lista las ausencias, de todos o de un empleado, ordenadas por fecha de inicio descendente.</summary>
    Task<IReadOnlyList<Ausencia>> ListarAsync(Guid? empleadoId, CancellationToken cancellationToken);

    /// <summary>Busca una ausencia por su identificador, con seguimiento de cambios; null si no existe.</summary>
    Task<Ausencia?> BuscarAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Agrega una ausencia nueva; se guarda con <see cref="GuardarCambiosAsync"/>.</summary>
    void Agregar(Ausencia ausencia);

    /// <summary>Guarda los cambios pendientes.</summary>
    Task GuardarCambiosAsync(CancellationToken cancellationToken);

    /// <summary>Indica si el empleado tiene una ausencia aprobada que cubra la fecha indicada (RN-PER-03).</summary>
    Task<bool> TieneAprobadaEnFechaAsync(Guid empleadoId, DateOnly fecha, CancellationToken cancellationToken);
}
