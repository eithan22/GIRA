using GIRA.Domain.Base;

namespace GIRA.Domain.Entities.Reservas;

/// <summary>
/// Mesa física dentro de una zona, con la capacidad que determina qué reservas puede recibir.
/// Solo Gerente y Administrador la gestionan.
/// </summary>
public class Mesa : BaseEntity
{
    /// <summary>Número o identificador visible de la mesa dentro de su zona.</summary>
    public int Numero { get; set; }

    /// <summary>Cantidad máxima de personas que la mesa puede recibir.</summary>
    public int Capacidad { get; set; }

    /// <summary>Zona a la que pertenece la mesa.</summary>
    public Guid ZonaId { get; set; }

    /// <summary>Si está activa, se ofrece en disponibilidad. Si es false, GI-163 la desactivó.</summary>
    public bool Activa { get; set; } = true;
}
