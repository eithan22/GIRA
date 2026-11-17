namespace GIRA.Application.DTOs.Reservas;

/// <summary>Detalle de una reserva para el personal (GI-164): cliente, mesa, franja, estado y motivo de cancelación.</summary>
/// <remarks>
/// <see cref="Estado"/> y <see cref="Origen"/> van como texto (ver <see cref="ReservaListadoDto"/>).
/// No incluye historial de estados (no existe todavía) ni el nombre de quien registró.
/// <see cref="PuedeModificar"/> y <see cref="PuedeCancelar"/> (GI-X, RN-RES-04) los calcula el backend con el
/// reloj del servidor (America/Santo_Domingo): la reserva está Confirmada y su franja todavía no empezó. El
/// frontend solo los consume para deshabilitar los botones; nunca los recalcula con la hora del navegador, y
/// el servidor sigue rechazando la operación igual si de todos modos se intenta.
/// </remarks>
public record ReservaDetalleDto(
    Guid Id,
    string Codigo,
    string Estado,
    DateOnly Fecha,
    Guid FranjaId,
    string FranjaNombre,
    TimeOnly FranjaHoraInicio,
    TimeOnly FranjaHoraFin,
    Guid MesaId,
    int MesaNumero,
    int MesaCapacidad,
    string ZonaNombre,
    int Personas,
    int? PersonasReales,
    DateTime? FechaCheckIn,
    Guid ClienteId,
    string ClienteNombre,
    string ClienteTelefono,
    string Origen,
    string? MotivoCancelacion,
    DateTime CreadaEn,
    bool PuedeModificar = false,
    bool PuedeCancelar = false);
