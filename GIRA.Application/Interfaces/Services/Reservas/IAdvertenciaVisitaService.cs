using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;

namespace GIRA.Application.Interfaces.Services.Reservas;

/// <summary>
/// Aviso (no bloqueante) de próxima reserva al registrar una visita sin reserva: una visita sentada al
/// final de una franja puede quedarse hasta la siguiente, donde esa mesa puede tener una reserva. El
/// sistema no bloquea el registro; solo avisa al personal, que decide.
/// </summary>
public interface IAdvertenciaVisitaService
{
    /// <summary>
    /// Si la mesa tiene una reserva vigente (Confirmada o EnCurso) en una franja posterior del mismo día
    /// que todavía no ha terminado, con la más cercana. Sin datos personales del cliente.
    /// ErrorType.Validation si la mesa, la franja o la fecha no son válidas; ErrorType.NotFound si la mesa o
    /// la franja no existen.
    /// </summary>
    Task<OperationResult<AdvertenciaMesaDto>> ConsultarAsync(
        ConsultarAdvertenciaMesaRequest request, CancellationToken cancellationToken);
}
