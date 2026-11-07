using GIRA.Domain.Base;
using GIRA.Domain.Enums.Reservas;

namespace GIRA.Domain.Entities.Reservas;

/// <summary>
/// Reserva de una mesa para una fecha y franja horaria determinadas.
/// No se elimina nunca; una cancelación se registra en la propia reserva (<see cref="MotivoCancelacion"/>).
/// </summary>
/// <remarks>
/// Esta entidad solo guarda los datos (GI-155). Las transiciones de estado válidas se
/// agregan como métodos en GI-156; hasta entonces <see cref="Estado"/> es una propiedad simple.
/// </remarks>
public class Reserva : BaseEntity
{
    /// <summary>Código único de 8 caracteres con el que el cliente consulta o cancela su reserva.</summary>
    public string Codigo { get; set; } = string.Empty;

    /// <summary>Cliente que hizo la reserva.</summary>
    public Guid ClienteId { get; set; }

    /// <summary>Mesa asignada automáticamente a la reserva.</summary>
    public Guid MesaId { get; set; }

    /// <summary>Franja horaria reservada.</summary>
    public Guid FranjaHorariaId { get; set; }

    /// <summary>Fecha de la reserva, en hora local de República Dominicana.</summary>
    public DateOnly Fecha { get; set; }

    /// <summary>Cantidad de personas para las que se reservó.</summary>
    public int Personas { get; set; }

    /// <summary>Estado actual de la reserva.</summary>
    public EstadoReserva Estado { get; set; }

    /// <summary>Canal por el que se registró la reserva.</summary>
    public OrigenReserva Origen { get; set; }

    /// <summary>Motivo de la cancelación; null si la reserva no fue cancelada.</summary>
    public string? MotivoCancelacion { get; set; }
}
