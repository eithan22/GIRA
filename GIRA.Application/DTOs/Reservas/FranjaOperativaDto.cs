namespace GIRA.Application.DTOs.Reservas;

/// <summary>
/// Franja horaria para el personal operativo (GI-399): solo lo necesario para filtros, mapa y
/// visitas. Es un contrato propio, separado de <see cref="FranjaDto"/> (administración), para que
/// uno pueda cambiar sin arrastrar al otro.
/// </summary>
/// <param name="Id">Identificador de la franja.</param>
/// <param name="Nombre">Nombre de la franja (por ejemplo, Almuerzo).</param>
/// <param name="HoraInicio">Hora en que empieza.</param>
/// <param name="HoraFin">Hora en que termina.</param>
/// <param name="Activa">false si la franja está desactivada; el front decide si la ofrece.</param>
public record FranjaOperativaDto(Guid Id, string Nombre, TimeOnly HoraInicio, TimeOnly HoraFin, bool Activa);
