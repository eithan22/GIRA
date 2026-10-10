using GIRA.Domain.Base;

namespace GIRA.Domain.Entities.Analitica;

/// <summary>Representa la afluencia real registrada para una fecha y hora.</summary>
public class HistorialAfluencia : BaseEntity
{
    /// <summary>Fecha en que se registró la afluencia.</summary>
    public DateOnly Fecha { get; set; }

    /// <summary>Hora de inicio de la franja observada.</summary>
    public TimeOnly HoraInicio { get; set; }

    /// <summary>Cantidad de comensales observados en la franja.</summary>
    public int CantidadComensales { get; set; }

    /// <summary>Reserva que originó el registro; null para observaciones importadas.</summary>
    public Guid? ReservaId { get; set; }
}
