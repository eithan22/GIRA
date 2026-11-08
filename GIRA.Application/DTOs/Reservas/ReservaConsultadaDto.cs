namespace GIRA.Application.DTOs.Reservas;

/// <summary>
/// Datos de una reserva consultada por código y teléfono. Sin identificadores internos
/// (ClienteId, MesaId) ni el motivo de cancelación (es una nota interna del personal, GI-223).
/// </summary>
/// <remarks>
/// Estado va como texto ("Confirmada", "Cancelada", ...), no como el enum: el proyecto no
/// tiene configurado <c>JsonStringEnumConverter</c> en ningún lado, así que un enum saldría
/// como número por defecto. Se resuelve local a este DTO, sin tocar la configuración global.
/// </remarks>
public record ReservaConsultadaDto(
    string Codigo,
    string NombreCliente,
    DateOnly Fecha,
    string FranjaNombre,
    TimeOnly FranjaHoraInicio,
    TimeOnly FranjaHoraFin,
    int Personas,
    string Estado);
