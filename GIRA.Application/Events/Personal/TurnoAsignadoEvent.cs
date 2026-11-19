using MediatR;

namespace GIRA.Application.Events.Personal;

/// <summary>
/// Se publica cuando se asigna un turno a un empleado (SRS 3.4). Notificaciones avisa al empleado.
/// Lleva solo identificadores y el horario, sin datos personales.
/// </summary>
public sealed record TurnoAsignadoEvent(
    Guid TurnoId,
    Guid EmpleadoId,
    DateOnly Fecha,
    TimeOnly HoraInicio,
    TimeOnly HoraFin) : INotification;
