using GIRA.Application.Common;
using GIRA.Application.DTOs.Personal;

namespace GIRA.Application.Interfaces.Services.Personal;

/// <summary>Casos de uso de cargos del personal (GI-296).</summary>
public interface ICargoService
{
    /// <summary>Lista los cargos ordenados por nombre.</summary>
    Task<OperationResult<IReadOnlyList<CargoDto>>> ListarAsync(CancellationToken cancellationToken);

    /// <summary>Obtiene un cargo por su identificador.</summary>
    Task<OperationResult<CargoDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Crea un cargo. Falla si el nombre ya existe.</summary>
    Task<OperationResult<CargoDto>> CrearAsync(CargoRequest request, CancellationToken cancellationToken);

    /// <summary>Edita un cargo. Falla si no existe o si el nuevo nombre ya lo usa otro cargo.</summary>
    Task<OperationResult<CargoDto>> ActualizarAsync(Guid id, CargoRequest request, CancellationToken cancellationToken);
}
