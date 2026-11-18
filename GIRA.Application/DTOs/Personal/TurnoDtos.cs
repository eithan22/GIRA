using GIRA.Domain.Enums.Personal;

namespace GIRA.Application.DTOs.Personal;

/// <summary>Turno tal como se entrega al cliente.</summary>
public sealed record TurnoDto(
    Guid Id,
    Guid EmpleadoId,
    Guid CargoId,
    DateOnly Fecha,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    EstadoTurno Estado,
    string? Notas);

/// <summary>Datos para asignar o modificar un turno.</summary>
public sealed record TurnoRequest(
    Guid EmpleadoId,
    Guid CargoId,
    DateOnly Fecha,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    string? Notas);

/// <summary>Cambio de estado de un turno.</summary>
/// <param name="Estado">Nuevo estado. Por ahora solo se acepta Cancelado (GI-307 agrega Completado y Ausente).</param>
public sealed record CambiarEstadoTurnoRequest(EstadoTurno Estado);
