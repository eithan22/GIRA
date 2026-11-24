using GIRA.Application.Common;
using GIRA.Application.DTOs.Personal;
using GIRA.Domain.Enums.Personal;

namespace GIRA.Application.Interfaces.Services.Personal;

/// <summary>Casos de uso de las ausencias de los empleados (GI-305, GI-355, GI-356).</summary>
public interface IAusenciaService
{
    /// <summary>Lista las ausencias; con <paramref name="empleadoId"/> solo las de ese empleado.</summary>
    Task<OperationResult<IReadOnlyList<AusenciaDto>>> ListarAsync(Guid? empleadoId, CancellationToken cancellationToken);

    /// <summary>Registra una solicitud de ausencia en estado Solicitada. Falla si el empleado no existe o las fechas no son válidas.</summary>
    Task<OperationResult<AusenciaDto>> CrearAsync(AusenciaRequest request, CancellationToken cancellationToken);

    /// <summary>Aprueba o rechaza una solicitud pendiente y guarda quién la resolvió.</summary>
    Task<OperationResult<AusenciaDto>> ResolverAsync(Guid id, EstadoAusencia estado, Guid resueltaPor, CancellationToken cancellationToken);
}
