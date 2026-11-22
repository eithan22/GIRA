using GIRA.Application.Common;
using GIRA.Application.DTOs.Personal;
using GIRA.Domain.Enums.Personal;

namespace GIRA.Application.Interfaces.Services.Personal;

/// <summary>Consulta y resolución de las sugerencias de personal (GI-304, RF-PER-10).</summary>
public interface ISugerenciaService
{
    /// <summary>Lista las sugerencias, opcionalmente filtradas por estado.</summary>
    Task<OperationResult<IReadOnlyList<SugerenciaDto>>> ListarAsync(EstadoSugerencia? estado, CancellationToken cancellationToken);

    /// <summary>
    /// Acepta o rechaza una sugerencia pendiente y registra quién la resolvió. No cambia turnos (RN-PER-06).
    /// Devuelve 409 si ya no está pendiente y 400 si el estado pedido no es Aceptada ni Rechazada.
    /// </summary>
    Task<OperationResult<SugerenciaDto>> ResolverAsync(Guid id, EstadoSugerencia nuevoEstado, Guid usuarioId, CancellationToken cancellationToken);
}
