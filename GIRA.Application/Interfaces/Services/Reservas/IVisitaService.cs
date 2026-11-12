using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;

namespace GIRA.Application.Interfaces.Services.Reservas;

/// <summary>Casos de uso de las visitas sin reserva (walk-ins, GI-166).</summary>
public interface IVisitaService
{
    /// <summary>
    /// Registra una visita sin reserva de hoy y publica <c>VisitaCompletadaEvent</c> con
    /// <c>ConReserva = false</c> (RN-RES-09). ErrorType.Validation si los datos no son válidos o
    /// la franja o la mesa no existen; ErrorType.Conflict si la franja está inactiva o no ha
    /// empezado, o si la mesa está inactiva, en zona inactiva o con reserva vigente en esa franja.
    /// </summary>
    Task<OperationResult<VisitaDto>> RegistrarAsync(RegistrarVisitaRequest request, CancellationToken cancellationToken);
}
