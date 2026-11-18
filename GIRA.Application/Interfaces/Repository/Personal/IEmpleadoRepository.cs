using GIRA.Domain.Entities.Personal;

namespace GIRA.Application.Interfaces.Repository.Personal;

/// <summary>Acceso a los empleados del restaurante (esquema <c>personal</c>).</summary>
public interface IEmpleadoRepository
{
    /// <summary>Lista los empleados ordenados por apellido y nombre; con <paramref name="soloActivos"/> excluye los inactivos.</summary>
    Task<IReadOnlyList<Empleado>> ListarAsync(bool soloActivos, CancellationToken cancellationToken);

    /// <summary>Busca un empleado por su identificador, con seguimiento de cambios; null si no existe.</summary>
    Task<Empleado?> BuscarAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Indica si ya existe un empleado vigente con esa cédula, ignorando <paramref name="excluirId"/>.</summary>
    Task<bool> ExisteCedulaAsync(string cedula, Guid? excluirId, CancellationToken cancellationToken);

    /// <summary>Agrega un empleado nuevo al contexto; se guarda con <see cref="GuardarCambiosAsync"/>.</summary>
    void Agregar(Empleado empleado);

    /// <summary>Guarda los cambios pendientes.</summary>
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}
