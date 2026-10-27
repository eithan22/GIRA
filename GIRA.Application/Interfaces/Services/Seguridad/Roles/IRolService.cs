using GIRA.Application.Common;
using GIRA.Application.DTOs.Seguridad.Roles;

namespace GIRA.Application.Interfaces.Services.Seguridad.Roles;

/// <summary>Consulta de los roles fijos del sistema.</summary>
public interface IRolService
{
    /// <summary>Lista los roles existentes ordenados por nombre (GET /api/roles).</summary>
    Task<OperationResult<IReadOnlyList<RolDto>>> ListarAsync(CancellationToken ct = default);
}
