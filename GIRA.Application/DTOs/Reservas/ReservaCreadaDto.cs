namespace GIRA.Application.DTOs.Reservas;

/// <summary>
/// Confirmación de la reserva creada. No incluye la mesa asignada ni su número: se asigna sola,
/// no es información que el endpoint público deba revelar. Sí incluye la zona (GI-4xx).
/// </summary>
public record ReservaCreadaDto(
    string Codigo,
    string NombreCliente,
    DateOnly Fecha,
    string FranjaNombre,
    TimeOnly FranjaHoraInicio,
    TimeOnly FranjaHoraFin,
    int Personas,
    string ZonaNombre);
