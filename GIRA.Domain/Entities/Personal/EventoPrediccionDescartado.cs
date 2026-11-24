using GIRA.Domain.Base;

namespace GIRA.Domain.Entities.Personal;

/// <summary>
/// Evento de predicción de afluencia que el consumidor de Personal rechazó (GI-303): sin identificador, con una versión
/// de contrato no soportada o de un restaurante distinto al configurado. Queda para revisión; no modifica sugerencias ni turnos.
/// </summary>
public class EventoPrediccionDescartado : BaseEntity
{
    /// <summary>Identificador del evento recibido, si venía.</summary>
    public Guid? EventoId { get; set; }

    /// <summary>Versión de contrato que traía el evento, si venía.</summary>
    public int? VersionContrato { get; set; }

    /// <summary>Restaurante que traía el evento, si venía.</summary>
    public Guid? RestauranteRecibidoId { get; set; }

    /// <summary>Motivo del rechazo, en texto corto.</summary>
    public string Motivo { get; set; } = string.Empty;

    /// <summary>Momento en que se recibió el evento, en UTC.</summary>
    public DateTime FechaRecepcion { get; set; }
}
