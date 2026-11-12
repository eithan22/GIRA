namespace GIRA.Application.DTOs.Reservas;

/// <summary>
/// Quién consulta o crea una reserva: define las reglas de horario (antelación mínima para el cliente,
/// franja no terminada para el personal) y la voz de los mensajes de error.
/// </summary>
public enum AudienciaReserva
{
    /// <summary>El cliente, desde el sitio web público.</summary>
    Cliente,

    /// <summary>El personal del restaurante (llamada o en persona).</summary>
    Personal
}
