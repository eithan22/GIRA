namespace GIRA.Application.DTOs.Reservas;

/// <summary>Datos para consultar una reserva por código y teléfono.</summary>
public record ConsultarReservaRequest(string Codigo, string Telefono);
