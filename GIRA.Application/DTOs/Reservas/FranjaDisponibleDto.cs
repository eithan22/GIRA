namespace GIRA.Application.DTOs.Reservas;

/// <summary>
/// Una franja del día con su disponibilidad (GI-226). Para el cliente llegan todas las franjas activas: las que no se
/// pueden elegir traen <see cref="Disponible"/> en false y el <see cref="MotivoNoDisponible"/> para mostrarlo; para
/// el personal solo llegan las seleccionables (siempre con <c>Disponible = true</c>). No informa cuántas mesas quedan
/// ni cuáles son: la mesa se asigna sola al reservar.
/// </summary>
/// <param name="Id">Id de la franja.</param>
/// <param name="Nombre">Nombre de la franja.</param>
/// <param name="HoraInicio">Hora de inicio (hora local).</param>
/// <param name="HoraFin">Hora de fin (hora local).</param>
/// <param name="ZonasDisponibles">
/// Zonas activas con al menos una mesa libre de capacidad suficiente en esta franja (GI-4xx, cliente). Vacía
/// si la franja no está disponible, o para el personal (que no elige zona por este DTO).
/// </param>
/// <param name="Disponible">Si la franja se puede elegir.</param>
/// <param name="MotivoNoDisponible">Por qué no se puede elegir; null si está disponible.</param>
public record FranjaDisponibleDto(
    Guid Id, string Nombre, TimeOnly HoraInicio, TimeOnly HoraFin,
    IReadOnlyList<ZonaDisponibleDto> ZonasDisponibles,
    bool Disponible = true, string? MotivoNoDisponible = null);
