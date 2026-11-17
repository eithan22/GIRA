namespace GIRA.Application.DTOs.Reservas;

/// <summary>Datos para cancelar una reserva por código y teléfono. Sin motivo: el cliente no está obligado a darlo.</summary>
public record CancelarReservaRequest(string Telefono);
