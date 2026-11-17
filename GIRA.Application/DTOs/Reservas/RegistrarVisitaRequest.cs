namespace GIRA.Application.DTOs.Reservas;

/// <summary>
/// Datos de una visita sin reserva (walk-in, GI-166). La fecha no se envía: la pone el servidor
/// (hoy, hora de Santo Domingo).
/// </summary>
/// <param name="FranjaHorariaId">Franja activa que ya empezó hoy.</param>
/// <param name="MesaId">
/// Mesa donde se sentó el grupo; obligatoria (decisión del líder, GI-166): null, vacía u omitida responde 400.
/// Debe estar activa, de zona activa, sin reserva vigente y sin otra visita en esa fecha y franja (una mesa
/// admite un solo grupo por franja). Se mantiene <see cref="Guid"/> nullable (no <see cref="Guid"/> a secas)
/// para que un cuerpo sin la clave <c>mesaId</c> y uno con <c>"mesaId": null</c> lleguen igual a
/// <c>null</c> y el validador los rechace con el mismo mensaje.
/// </param>
/// <param name="Personas">Personas atendidas; hasta la capacidad de la mesa.</param>
public record RegistrarVisitaRequest(Guid FranjaHorariaId, Guid? MesaId, int Personas);
