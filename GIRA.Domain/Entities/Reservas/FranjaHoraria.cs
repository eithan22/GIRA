using GIRA.Domain.Base;

namespace GIRA.Domain.Entities.Reservas;

/// <summary>
/// Bloque horario en el que se puede reservar (por ejemplo, almuerzo o cena).
/// Se siembra siempre en la migración, en todos los ambientes.
/// </summary>
public class FranjaHoraria : BaseEntity
{
    /// <summary>Nombre de la franja.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Hora de inicio de la franja, en hora local de República Dominicana.</summary>
    public TimeOnly HoraInicio { get; set; }

    /// <summary>Hora de fin de la franja, en hora local de República Dominicana.</summary>
    public TimeOnly HoraFin { get; set; }
}
