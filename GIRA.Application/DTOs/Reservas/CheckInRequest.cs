namespace GIRA.Application.DTOs.Reservas;

/// <summary>Datos del check-in de una reserva (GI-165): cuántas personas llegaron en realidad.</summary>
/// <param name="PersonasReales">Personas que llegaron; mínimo 1 y no más que la capacidad de la mesa.</param>
public record CheckInRequest(int PersonasReales);
