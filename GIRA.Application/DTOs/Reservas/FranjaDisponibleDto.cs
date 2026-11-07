namespace GIRA.Application.DTOs.Reservas;

/// <summary>
/// Una franja con al menos una mesa disponible. No informa cuántas mesas quedan ni
/// cuáles son: la mesa se asigna sola al reservar.
/// </summary>
public record FranjaDisponibleDto(Guid Id, string Nombre, TimeOnly HoraInicio, TimeOnly HoraFin);
