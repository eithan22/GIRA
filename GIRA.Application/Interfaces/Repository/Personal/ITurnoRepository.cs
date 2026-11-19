using GIRA.Domain.Entities.Personal;

namespace GIRA.Application.Interfaces.Repository.Personal;

/// <summary>Acceso a los turnos asignados a los empleados (esquema <c>personal</c>).</summary>
public interface ITurnoRepository
{
    /// <summary>Busca un turno por su identificador, con seguimiento de cambios; null si no existe.</summary>
    Task<Turno?> BuscarAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Lista los turnos de un empleado en una fecha, sin seguimiento de cambios.</summary>
    Task<IReadOnlyList<Turno>> ListarPorEmpleadoYFechaAsync(Guid empleadoId, DateOnly fecha, CancellationToken cancellationToken);

    /// <summary>Lista los turnos de un empleado entre dos fechas (inclusive), sin seguimiento de cambios.</summary>
    Task<IReadOnlyList<Turno>> ListarPorEmpleadoEntreAsync(Guid empleadoId, DateOnly desde, DateOnly hasta, CancellationToken cancellationToken);

    /// <summary>Lista los turnos activos (Programado o Confirmado) de un empleado desde una fecha, con seguimiento de cambios.</summary>
    Task<IReadOnlyList<Turno>> ListarFuturosActivosAsync(Guid empleadoId, DateOnly desde, CancellationToken cancellationToken);

    /// <summary>Agrega un turno nuevo al contexto; se guarda con <see cref="GuardarCambiosAsync"/>.</summary>
    void Agregar(Turno turno);

    /// <summary>Guarda los cambios pendientes.</summary>
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}
