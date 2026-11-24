using GIRA.Domain.Base;

namespace GIRA.Domain.Entities.Personal;

/// <summary>
/// Bloque de horas en que un empleado puede trabajar un día de la semana (RF-PER-03).
/// Un empleado puede tener varios bloques por día, pero no pueden solaparse (RN-PER-01).
/// </summary>
public class Disponibilidad : BaseEntity
{
    /// <summary>Empleado al que pertenece el bloque.</summary>
    public Guid EmpleadoId { get; set; }

    /// <summary>Día de la semana del bloque.</summary>
    public DayOfWeek DiaSemana { get; set; }

    /// <summary>Hora en que empieza el bloque.</summary>
    public TimeOnly HoraInicio { get; set; }

    /// <summary>Hora en que termina el bloque; siempre posterior a <see cref="HoraInicio"/>.</summary>
    public TimeOnly HoraFin { get; set; }
}
