using GIRA.Domain.Base;

namespace GIRA.Domain.Entities.Inventario;

/// <summary>Unidad en la que se mide un insumo (kg, g, l, ml, unidad).</summary>
public class UnidadMedida : BaseEntity
{
    /// <summary>Nombre de la unidad, por ejemplo "Kilogramo".</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Abreviatura que acompaña a las cantidades, por ejemplo "kg".</summary>
    public string Abreviatura { get; set; } = string.Empty;
}
