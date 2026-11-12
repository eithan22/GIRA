namespace GIRA.Application.DTOs.Reservas;

/// <summary>Detalle de una reserva para el personal (GI-164): cliente, mesa, franja, estado y motivo de cancelación.</summary>
/// <remarks>
/// <see cref="Estado"/> y <see cref="Origen"/> van como texto (ver <see cref="ReservaListadoDto"/>).
/// No incluye historial de estados (no existe todavía) ni el nombre de quien registró.
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
    DateTime CreadaEn);
