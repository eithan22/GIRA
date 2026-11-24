using GIRA.Domain.Base;

namespace GIRA.Domain.Entities.Personal;

/// <summary>
/// Cuántos comensales atiende un empleado de un cargo y cuál es el mínimo por franja (SRS 5.4, RF-PER-08).
/// Hay un solo requerimiento por cargo. Sirve para calcular el personal requerido en las sugerencias.
/// </summary>
public class RequerimientoPersonal : BaseEntity
{
    /// <summary>Cargo al que aplica el requerimiento.</summary>
    public Guid CargoId { get; set; }

    /// <summary>Comensales que atiende un empleado de este cargo en una franja. Mayor que cero.</summary>
    public int ComensalesPorEmpleado { get; set; }

    /// <summary>Mínimo de personas de este cargo por franja, aunque la afluencia sea baja. Mayor que cero.</summary>
    public int MinimoPorFranja { get; set; }
}
