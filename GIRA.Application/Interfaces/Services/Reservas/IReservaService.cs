using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;

namespace GIRA.Application.Interfaces.Services.Reservas;

/// <summary>Casos de uso de creación y gestión de reservas.</summary>
public interface IReservaService
{
    /// <summary>Crea una reserva como invitado, asignando la mesa automáticamente.</summary>
    Task<OperationResult<ReservaCreadaDto>> CrearAsync(CrearReservaRequest request, CancellationToken cancellationToken);
}
