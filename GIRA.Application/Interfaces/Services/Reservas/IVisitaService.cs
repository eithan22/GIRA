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

    /// <summary>
    /// Visitas sin reserva de un día (GI-402): totales de todo el día y las 200 más recientes, con la hora de
    /// registro en hora local de Santo Domingo. Sin fecha, hoy. ErrorType.Validation si la fecha es futura o de hace
    /// más de 365 días. Sin datos personales. Solo lee: no tiene nada que ver con
    /// <c>VisitaCompletadaEvent</c> ni cambia el estado de las mesas (la visita no marca la mesa como ocupada).
    /// </summary>
    Task<OperationResult<VisitasDelDiaDto>> ListarAsync(ListarVisitasRequest request, CancellationToken cancellationToken);
}
