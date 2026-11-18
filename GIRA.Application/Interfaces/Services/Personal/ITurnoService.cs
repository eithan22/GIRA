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
}
