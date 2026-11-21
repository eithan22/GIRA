using GIRA.Domain.Base;

namespace GIRA.Domain.Entities.Inventario;

/// <summary>Insumos que lleva un plato. Una receta por plato.</summary>
public class Receta : BaseEntity
{
    /// <summary>Plato al que pertenece (único).</summary>
    public Guid PlatoId { get; set; }

    /// <summary>Porciones que rinden las cantidades de <see cref="Detalles"/>.</summary>
    public int Porciones { get; set; }

    /// <summary>Pasos de preparación; opcional.</summary>
    public string? Instrucciones { get; set; }

    /// <summary>Líneas de la receta.</summary>
    public List<RecetaDetalle> Detalles { get; set; } = [];
}
