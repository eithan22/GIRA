using GIRA.Domain.Base;

namespace GIRA.Domain.Entities.Reservas;

/// <summary>
/// Visita de un cliente que llegó sin reserva previa (walk-in) y ocupó una mesa.
/// No está asociada a un <see cref="Cliente"/>: solo registra la mesa, la franja y
/// cuántas personas se atendieron, para la afluencia del restaurante.
/// </summary>
/// <remarks>
/// Decisión de diseño (GI-402, GI-404): se evaluó agregar un estado o una <c>FechaFin</c> para "terminar" la
/// visita y se decidió NO hacerlo. El modelo actual trata la visita como un registro ya consumado (el grupo
/// llegó, se le atendió y se anotó), no como una sesión con ciclo de vida: no hay nada que cerrar y el proceso
/// automático de reservas no la toca. La consecuencia aceptada es que la visita ocupa su mesa durante toda su
/// franja a efectos de reservas y disponibilidad, y se libera al cambiar de franja.
/// </remarks>
public class VisitaSinReserva : BaseEntity
{
    /// <summary>Fecha de la visita, en hora local de República Dominicana.</summary>
    public DateOnly Fecha { get; set; }

    /// <summary>Franja horaria en la que ocurrió la visita.</summary>
    public Guid FranjaHorariaId { get; set; }

    /// <summary>Mesa ocupada por la visita; null si no se le asignó mesa (SRS 5.2: MesaId opcional).</summary>
    public Guid? MesaId { get; set; }

    /// <summary>Cantidad real de personas atendidas.</summary>
    public int PersonasReales { get; set; }
}
