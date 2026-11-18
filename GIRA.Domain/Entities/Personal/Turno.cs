using GIRA.Domain.Base;
using GIRA.Domain.Enums.Personal;

namespace GIRA.Domain.Entities.Personal;

/// <summary>
/// Turno asignado a un empleado en una fecha y horario, para un cargo (SRS 5.4, RF-PER-04).
/// El turno no cruza la medianoche: la hora de fin es posterior a la de inicio el mismo día.
/// La concurrencia (RowVersion) la aplica la convención de BaseEntity del proyecto.
/// </summary>
public class Turno : BaseEntity
{
    /// <summary>Empleado asignado.</summary>
    public Guid EmpleadoId { get; set; }

    /// <summary>Cargo con el que se asigna el turno.</summary>
    public Guid CargoId { get; set; }

    /// <summary>Fecha del turno.</summary>
    public DateOnly Fecha { get; set; }

    /// <summary>Hora de inicio.</summary>
    public TimeOnly HoraInicio { get; set; }

    /// <summary>Hora de fin; posterior a <see cref="HoraInicio"/>.</summary>
    public TimeOnly HoraFin { get; set; }

    /// <summary>Estado del turno.</summary>
    public EstadoTurno Estado { get; set; } = EstadoTurno.Programado;

    /// <summary>Notas opcionales del supervisor.</summary>
    public string? Notas { get; set; }
}
