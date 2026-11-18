using GIRA.Application.Common;
using GIRA.Application.DTOs.Personal;

namespace GIRA.Application.Interfaces.Services.Personal;

/// <summary>Casos de uso de empleados del personal (GI-295, GI-315, GI-316).</summary>
public interface IEmpleadoService
{
    /// <summary>Lista los empleados; con <paramref name="soloActivos"/> excluye los inactivos.</summary>
    Task<OperationResult<IReadOnlyList<EmpleadoDto>>> ListarAsync(bool soloActivos, CancellationToken cancellationToken);

    /// <summary>Registra un empleado. Falla si la cédula ya existe o el cargo no existe.</summary>
    Task<OperationResult<EmpleadoDto>> CrearAsync(EmpleadoRequest request, CancellationToken cancellationToken);

    /// <summary>Edita un empleado. Falla si no existe, si la cédula la usa otro o si el cargo no existe.</summary>
    Task<OperationResult<EmpleadoDto>> ActualizarAsync(Guid id, EmpleadoRequest request, CancellationToken cancellationToken);

    /// <summary>Activa o desactiva un empleado. No lo elimina.</summary>
    Task<OperationResult<EmpleadoDto>> CambiarEstadoAsync(Guid id, bool activo, CancellationToken cancellationToken);
}
