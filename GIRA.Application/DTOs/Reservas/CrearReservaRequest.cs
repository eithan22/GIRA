namespace GIRA.Application.DTOs.Reservas;

/// <summary>
/// Datos para crear una reserva. Sin Origen, UsuarioId ni Estado: el servicio fija
/// Origen = Web en esta historia (GI-158); GI-222 extenderá el camino con token.
/// </summary>
public record CrearReservaRequest(string Nombre, string Telefono, DateOnly Fecha, Guid FranjaHorariaId, int Personas);
