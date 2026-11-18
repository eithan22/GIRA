using GIRA.Application.Common;
using GIRA.Application.DTOs.Personal;

namespace GIRA.Application.Interfaces.Services.Personal;

/// <summary>Casos de uso de los requerimientos de personal por cargo (GI-302, GI-342, GI-343).</summary>
public interface IRequerimientoPersonalService
{
    /// <summary>Lista los requerimientos configurados.</summary>
    Task<OperationResult<IReadOnlyList<RequerimientoPersonalDto>>> ListarAsync(CancellationToken cancellationToken);

    /// <summary>Crea el requerimiento del cargo o actualiza el que ya existe. Falla si el cargo no existe.</summary>
    Task<OperationResult<RequerimientoPersonalDto>> GuardarAsync(RequerimientoPersonalRequest request, CancellationToken cancellationToken);
}
