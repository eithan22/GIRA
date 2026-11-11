using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Domain.Enums.Reservas;

namespace GIRA.Application.Interfaces.Services.Reservas;

/// <summary>Casos de uso de creación y gestión de reservas.</summary>
public interface IReservaService
{
    /// <summary>Crea una reserva como invitado, asignando la mesa automáticamente.</summary>
    Task<OperationResult<ReservaCreadaDto>> CrearAsync(CrearReservaRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Crea una reserva registrada por el personal (GI-222/GI-235), con las mismas
    /// validaciones y asignación de mesa que <see cref="CrearAsync"/>. Si
    /// <paramref name="origen"/> no es <see cref="OrigenReserva.Telefono"/> ni
    /// <see cref="OrigenReserva.Presencial"/>, devuelve ErrorType.Validation (el controller
    /// ya debe garantizar uno de los dos antes de llamar; esto es una segunda red de
    /// seguridad, no el único lugar donde se valida). El usuario que registra no se recibe
    /// aquí: lo toma BaseEntity.CreatedBy del token, vía AuditoriaSaveChangesInterceptor.
    /// </summary>
    Task<OperationResult<ReservaCreadaDto>> CrearComoPersonalAsync(
        CrearReservaRequest request, OrigenReserva origen, CancellationToken cancellationToken);

    /// <summary>
    /// Agenda del personal (GI-164): lista paginada filtrable por fecha, estado, franja y
    /// teléfono. Sin fecha ni teléfono usa el día de hoy (hora de Santo Domingo). ErrorType.Validation
    /// si la paginación, el estado o el teléfono no son válidos.
    /// </summary>
    Task<OperationResult<PagedResult<ReservaListadoDto>>> ListarAsync(ListarReservasRequest request, CancellationToken cancellationToken);

    /// <summary>Detalle de una reserva para el personal (GI-164). ErrorType.NotFound si no existe.</summary>
    Task<OperationResult<ReservaDetalleDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Modificación por el personal (GI-160, RN-RES-04 y RN-RES-05): cambia fecha, franja, personas
    /// y/o mesa de una reserva Confirmada, revalidando la disponibilidad como si fuera nueva (sin
    /// contar la propia reserva) y publicando <c>ReservaModificadaEvent</c> una sola vez tras guardar.
    /// Solo hasta el inicio de la franja original. ErrorType.Validation si los datos no son válidos,
    /// ErrorType.NotFound si no existe y ErrorType.Conflict si no está Confirmada, la franja original
    /// ya empezó, no hay mesa libre o hubo una modificación concurrente.
    /// </summary>
    Task<OperationResult<ReservaDetalleDto>> ModificarAsync(Guid id, ModificarReservaRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Cancelación por el personal (GI-223, RN-RES-04): pasa de Confirmada a Cancelada guardando el
    /// motivo (obligatorio, de 1 a 500 caracteres) en la propia reserva, que nunca se elimina. El
    /// personal puede cancelar hasta el inicio de la franja (no aplica el límite del cliente).
    /// ErrorType.Validation si el motivo no es válido, ErrorType.NotFound si no existe y
    /// ErrorType.Conflict si no está Confirmada, la franja ya empezó o hubo una modificación concurrente.
    /// </summary>
    Task<OperationResult<ReservaDetalleDto>> CancelarPorPersonalAsync(Guid id, CancelarPorPersonalRequest request, CancellationToken cancellationToken);

    /// <summary>
    /// Completa la visita (GI-166, RN-RES-08): pasa de EnCurso a Completada y publica una sola vez
    /// <c>VisitaCompletadaEvent</c> (con las personas reales de la reserva y <c>ConReserva = true</c>)
    /// después de guardar. Sin ventana horaria. ErrorType.NotFound si no existe; ErrorType.Conflict
    /// si no está EnCurso o hubo una modificación concurrente. Un handler que falla se registra y
    /// no revierte la operación.
    /// </summary>
    Task<OperationResult<ReservaDetalleDto>> CompletarAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Check-in de una reserva (GI-165, RN-RES-06 y RN-RES-15): pasa de Confirmada a EnCurso y
    /// guarda las personas reales y la hora. Solo desde la ventana configurable antes del inicio
    /// de la franja y hasta que la franja termina. ErrorType.NotFound si no existe,
    /// ErrorType.Validation si las personas son menos de 1 o superan la capacidad de la mesa, y
    /// ErrorType.Conflict si el estado, la hora o una modificación concurrente lo impiden.
    /// No publica eventos (el SRS no define uno para el check-in).
    /// </summary>
    Task<OperationResult<ReservaDetalleDto>> CheckInAsync(Guid id, CheckInRequest request, CancellationToken cancellationToken);

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
