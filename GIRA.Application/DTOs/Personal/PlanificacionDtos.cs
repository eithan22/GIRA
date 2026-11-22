namespace GIRA.Application.DTOs.Personal;

/// <summary>
/// Cobertura de un cargo en una franja de un día: cuántos turnos Programados o Confirmados la cubren completa (GI-301, RF-PER-07).
/// </summary>
/// <param name="Fecha">Día de la cobertura.</param>
/// <param name="FranjaHorariaId">Identificador de la franja, del módulo de Reservas.</param>
/// <param name="FranjaNombre">Nombre de la franja.</param>
/// <param name="HoraInicio">Inicio de la franja.</param>
/// <param name="HoraFin">Fin de la franja.</param>
/// <param name="CargoId">Identificador del cargo.</param>
/// <param name="CargoNombre">Nombre del cargo.</param>
/// <param name="PersonalPlanificado">Cantidad de turnos del cargo que cubren la franja completa.</param>
public record CoberturaFranjaDto(
    DateOnly Fecha,
    Guid FranjaHorariaId,
    string FranjaNombre,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    Guid CargoId,
    string CargoNombre,
    int PersonalPlanificado);
