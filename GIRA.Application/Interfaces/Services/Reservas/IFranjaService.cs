using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;

namespace GIRA.Application.Interfaces.Services.Reservas;

/// <summary>Casos de uso de administración de franjas horarias (GI-195).</summary>
public interface IFranjaService
{
    /// <summary>Lista franjas con paginación y filtro opcional por estado.</summary>
    Task<OperationResult<PagedResult<FranjaDto>>> ListarAsync(ListarSalonRequest request, CancellationToken cancellationToken);

    /// <summary>Obtiene una franja por id.</summary>
    Task<OperationResult<FranjaDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Crea una franja validando horario, nombre único y solapamientos.</summary>
    Task<OperationResult<FranjaDto>> CrearAsync(GuardarFranjaRequest request, CancellationToken cancellationToken);

    /// <summary>Actualiza una franja; no permite cambiar el horario si hay reservas futuras.</summary>
    Task<OperationResult<FranjaDto>> ActualizarAsync(Guid id, GuardarFranjaRequest request, CancellationToken cancellationToken);

    /// <summary>Activa o desactiva una franja; no permite desactivarla si hay reservas futuras.</summary>
    Task<OperationResult<FranjaDto>> CambiarEstadoAsync(Guid id, bool activa, CancellationToken cancellationToken);
}
