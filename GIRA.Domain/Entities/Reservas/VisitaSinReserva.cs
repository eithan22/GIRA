using GIRA.Domain.Base;

namespace GIRA.Domain.Entities.Reservas;

/// <summary>
/// Visita de un cliente que llegó sin reserva previa (walk-in) y ocupó una mesa.
/// No está asociada a un <see cref="Cliente"/>: solo registra la mesa, la franja y
/// cuántas personas se atendieron, para la afluencia del restaurante.
/// </summary>
public class VisitaSinReserva : BaseEntity
{
    /// <summary>Fecha de la visita, en hora local de República Dominicana.</summary>
    public DateOnly Fecha { get; set; }

    /// <summary>Franja horaria en la que ocurrió la visita.</summary>
    public Guid FranjaHorariaId { get; set; }

    /// <summary>Mesa ocupada por la visita.</summary>
    public Guid MesaId { get; set; }

    /// <summary>Cantidad real de personas atendidas.</summary>
    public int PersonasReales { get; set; }
}
