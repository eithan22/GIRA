namespace GIRA.Application.DTOs.Reservas;

/// <summary>
/// Confirmación de la reserva creada. No incluye la mesa asignada: se asigna sola,
/// no es información que el endpoint público deba revelar.
/// </summary>
public record ReservaCreadaDto(
    string Codigo,
    string NombreCliente,
    DateOnly Fecha,
    string FranjaNombre,
    TimeOnly FranjaHoraInicio,
    TimeOnly FranjaHoraFin,
    int Personas);
