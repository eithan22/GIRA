using GIRA.Domain.Base;

namespace GIRA.Domain.Entities.Configuracion;

/// <summary>Horario de operación del restaurante para un día de la semana (una fila por día).</summary>
public class HorarioOperacion : BaseEntity
{
    /// <summary>Día de la semana al que aplica.</summary>
    public DayOfWeek DiaSemana { get; set; }

    /// <summary>Si el restaurante abre ese día de la semana.</summary>
    public bool Abierto { get; set; }

    /// <summary>Hora de apertura, en hora local de República Dominicana.</summary>
    public TimeOnly HoraApertura { get; set; }

    /// <summary>Hora de cierre, en hora local de República Dominicana.</summary>
    public TimeOnly HoraCierre { get; set; }
}
