namespace GIRA.Application.DTOs.Personal;

/// <summary>Bloque de disponibilidad tal como se entrega al cliente.</summary>
public sealed record DisponibilidadDto(
    Guid Id,
    Guid EmpleadoId,
    DayOfWeek DiaSemana,
    TimeOnly HoraInicio,
    TimeOnly HoraFin);

/// <summary>Datos para registrar un bloque de disponibilidad.</summary>
public sealed record DisponibilidadRequest(DayOfWeek DiaSemana, TimeOnly HoraInicio, TimeOnly HoraFin);
