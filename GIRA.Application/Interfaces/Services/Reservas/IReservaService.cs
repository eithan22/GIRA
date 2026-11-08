using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;

namespace GIRA.Application.Interfaces.Services.Reservas;

/// <summary>Casos de uso de creación y gestión de reservas.</summary>
public interface IReservaService
{
    /// <summary>Crea una reserva como invitado, asignando la mesa automáticamente.</summary>
    Task<OperationResult<ReservaCreadaDto>> CrearAsync(CrearReservaRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Consulta una reserva por código y teléfono. Si no coinciden los dos, devuelve
    /// ErrorType.NotFound con el mismo mensaje, sin revelar cuál estaba mal.
    /// </summary>
    Task<OperationResult<ReservaConsultadaDto>> ConsultarPorCodigoAsync(ConsultarReservaRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Cancela una reserva por código y teléfono, sin motivo (el cliente no está obligado a
    /// darlo). Mismo 404 que ConsultarPorCodigoAsync si no coinciden; Conflict si ya no se
    /// puede cancelar (estado o límite de anticipación).
    /// </summary>
    Task<OperationResult> CancelarPorCodigoAsync(string codigo, CancelarReservaRequest request, CancellationToken cancellationToken);
}
