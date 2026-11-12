namespace GIRA.Application.DTOs.Reservas;

/// <summary>Datos para que el personal cancele una reserva (GI-223): el motivo es obligatorio.</summary>
/// <param name="Motivo">Motivo de la cancelación; se recorta y debe tener entre 1 y 500 caracteres.</param>
public record CancelarPorPersonalRequest(string Motivo);
