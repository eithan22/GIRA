namespace GIRA.Application.Interfaces.Repository.Personal;

/// <summary>Consultas sobre las ausencias de los empleados (esquema <c>personal</c>).</summary>
public interface IAusenciaRepository
{
    /// <summary>Indica si el empleado tiene una ausencia aprobada que cubra la fecha indicada (RN-PER-03).</summary>
    Task<bool> TieneAprobadaEnFechaAsync(Guid empleadoId, DateOnly fecha, CancellationToken cancellationToken);
}
