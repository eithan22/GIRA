using MediatR;

namespace GIRA.Application.Events.Personal;

/// <summary>
/// Se publica cuando cambia un turno: horario, estado o cancelación por desactivación del empleado (SRS 3.4).
/// Notificaciones avisa al empleado. Lleva solo el identificador, el empleado y una descripción corta del cambio.
/// </summary>
public sealed record TurnoModificadoEvent(
    Guid TurnoId,
    Guid EmpleadoId,
    string Cambio) : INotification;
