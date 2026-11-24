using GIRA.Application.Common;
using GIRA.Application.DTOs.Personal;
using GIRA.Domain.Enums.Personal;

namespace GIRA.Application.Interfaces.Services.Personal;

/// <summary>Casos de uso de turnos (GI-298, GI-326 a GI-328).</summary>
public interface ITurnoService
{
    /// <summary>Asigna un turno. Revisa disponibilidad, solapamiento y ausencias aprobadas.</summary>
    Task<OperationResult<TurnoDto>> CrearAsync(TurnoRequest request, CancellationToken cancellationToken);

    /// <summary>Modifica un turno vigente. Los turnos pasados o cancelados no se modifican (RN-PER-10).</summary>
    Task<OperationResult<TurnoDto>> ActualizarAsync(Guid id, TurnoRequest request, CancellationToken cancellationToken);

    /// <summary>Cambia el estado de un turno. Por ahora solo acepta Cancelado.</summary>
    Task<OperationResult<TurnoDto>> CambiarEstadoAsync(Guid id, EstadoTurno estado, CancellationToken cancellationToken);

    /// <summary>
    /// Lista los turnos entre dos fechas (inclusive), de todos los estados, con filtro opcional por cargo (GI-338, RF-PER-04).
    /// Rango máximo de 31 días. Devuelve 404 si el cargo indicado no existe.
    /// </summary>
    Task<OperationResult<IReadOnlyList<TurnoDto>>> ListarAsync(DateOnly desde, DateOnly hasta, Guid? cargoId, CancellationToken cancellationToken);

    /// <summary>
    /// Lista solo los turnos de un empleado entre dos fechas (GI-300, RF-PER-06, RN-PER-09). Rango máximo de 31 días.
    /// El empleado lo decide quien llama: nunca viene de un parámetro del cliente.
    /// </summary>
    Task<OperationResult<IReadOnlyList<TurnoDto>>> ListarDeEmpleadoAsync(Guid empleadoId, DateOnly desde, DateOnly hasta, CancellationToken cancellationToken);
}
