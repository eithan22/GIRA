using GIRA.Application.Common;
using GIRA.Application.DTOs.Personal;

namespace GIRA.Application.Interfaces.Services.Personal;

/// <summary>Casos de uso de la disponibilidad semanal de los empleados (GI-297, GI-319).</summary>
public interface IDisponibilidadService
{
    /// <summary>Lista los bloques de disponibilidad de un empleado.</summary>
    Task<OperationResult<IReadOnlyList<DisponibilidadDto>>> ListarAsync(Guid empleadoId, CancellationToken cancellationToken);

    /// <summary>Registra un bloque. Falla si el empleado no existe, si las horas son inválidas o si se solapa con otro bloque del mismo día.</summary>
    Task<OperationResult<DisponibilidadDto>> AgregarAsync(Guid empleadoId, DisponibilidadRequest request, CancellationToken cancellationToken);

    /// <summary>Elimina un bloque (borrado lógico). Falla si no existe o no pertenece al empleado indicado.</summary>
    Task<OperationResult> EliminarAsync(Guid empleadoId, Guid id, CancellationToken cancellationToken);
}
