namespace GIRA.Application.DTOs.Reservas;

/// <summary>
/// Datos de una visita sin reserva (walk-in, GI-166). La fecha no se envía: la pone el servidor
/// (hoy, hora de Santo Domingo).
/// </summary>
/// <param name="FranjaHorariaId">Franja activa que ya empezó hoy.</param>
/// <param name="MesaId">Mesa ocupada; opcional. Si se envía debe estar activa y libre en esa franja.</param>
/// <param name="Personas">Personas atendidas; sin mesa, hasta el tope de capacidad de una mesa (50).</param>
public record RegistrarVisitaRequest(Guid FranjaHorariaId, Guid? MesaId, int Personas);
