using GIRA.Domain.Base;

namespace GIRA.Domain.Entities.Reservas;

/// <summary>
/// Área física del restaurante que agrupa mesas (por ejemplo, salón principal o terraza).
/// Solo Gerente y Administrador la gestionan.
/// </summary>
public class Zona : BaseEntity
{
    /// <summary>Nombre de la zona.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Si es false, ninguna mesa de esta zona se ofrece en disponibilidad, aunque cada mesa esté activa (RN-RES-21).</summary>
    public bool Activa { get; set; } = true;
}
