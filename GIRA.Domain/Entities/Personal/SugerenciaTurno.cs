using GIRA.Domain.Base;
using GIRA.Domain.Enums.Personal;

namespace GIRA.Domain.Entities.Personal;

/// <summary>
/// Sugerencia de personal para una fecha, franja y cargo, cuando el personal planificado difiere del requerido
/// según la predicción de afluencia (SRS 5.4, RF-PER-09). No cambia turnos: el gerente o el supervisor decide.
/// </summary>
public class SugerenciaTurno : BaseEntity
{
    /// <summary>Día de la sugerencia.</summary>
    public DateOnly Fecha { get; set; }

    /// <summary>Franja horaria, del módulo de Reservas.</summary>
    public Guid FranjaHorariaId { get; set; }

    /// <summary>Cargo al que se refiere la sugerencia.</summary>
    public Guid CargoId { get; set; }

    /// <summary>Turnos Programados o Confirmados que cubren la franja completa.</summary>
    public int PersonalPlanificado { get; set; }

    /// <summary>Personal requerido: el mayor entre el mínimo del cargo y afluencia ÷ comensales por empleado, redondeado hacia arriba.</summary>
    public int PersonalRequerido { get; set; }

    /// <summary>Requerido menos planificado. Positivo: faltan personas. Negativo: sobran.</summary>
    public int Diferencia { get; set; }

    /// <summary>
    /// Predicción que originó la sugerencia. Null mientras el evento de Analítica no incluya su identificador
    /// (pendiente de acordar con Isaac).
    /// </summary>
    public Guid? PrediccionId { get; set; }

    /// <summary>Estado de la sugerencia.</summary>
    public EstadoSugerencia Estado { get; set; } = EstadoSugerencia.Pendiente;

    /// <summary>Momento en que se generó, en UTC.</summary>
    public DateTime FechaGeneracion { get; set; }

    /// <summary>Usuario que la aceptó o rechazó; null mientras esté pendiente o vencida.</summary>
    public Guid? ResueltaPor { get; set; }
}
