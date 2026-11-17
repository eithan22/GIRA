using GIRA.Domain.Entities.Reservas;

namespace GIRA.Application.Interfaces.Repository.Reservas;

/// <summary>
/// Reserva (con seguimiento de cambios) junto con la hora de inicio y de fin de su franja. Reserva solo
/// guarda la clave de la franja, y los procesos automáticos necesitan esas horas para decidir si la franja
/// ya terminó (fin: no-show GI-161/GI-404 y cierre de visitas GI-224).
/// </summary>
/// <param name="Reserva">La reserva, lista para ser modificada y guardada.</param>
/// <param name="HoraInicio">Hora local de inicio de su franja.</param>
/// <param name="HoraFin">Hora local de fin de su franja.</param>
public sealed record ReservaConFranja(Reserva Reserva, TimeOnly HoraInicio, TimeOnly HoraFin);
