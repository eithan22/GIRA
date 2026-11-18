using GIRA.Domain.Entities.Personal;

namespace GIRA.Application.Interfaces.Repository.Personal;

/// <summary>Acceso a los bloques de disponibilidad de los empleados (esquema <c>personal</c>).</summary>
public interface IDisponibilidadRepository
{
    /// <summary>Lista los bloques de un empleado ordenados por día y hora de inicio, sin seguimiento de cambios.</summary>
    Task<IReadOnlyList<Disponibilidad>> ListarPorEmpleadoAsync(Guid empleadoId, CancellationToken cancellationToken);

    /// <summary>Busca un bloque por su identificador, con seguimiento de cambios; null si no existe.</summary>
    Task<Disponibilidad?> BuscarAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Agrega un bloque nuevo al contexto; se guarda con <see cref="GuardarCambiosAsync"/>.</summary>
    void Agregar(Disponibilidad disponibilidad);

    /// <summary>Marca un bloque para borrado lógico; se guarda con <see cref="GuardarCambiosAsync"/>.</summary>
    void Eliminar(Disponibilidad disponibilidad);

    /// <summary>Guarda los cambios pendientes.</summary>
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}
