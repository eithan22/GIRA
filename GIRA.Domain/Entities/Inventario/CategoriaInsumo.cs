using GIRA.Domain.Base;

namespace GIRA.Domain.Entities.Inventario;

/// <summary>Agrupa insumos parecidos (carnes, lácteos, vegetales, bebidas).</summary>
public class CategoriaInsumo : BaseEntity
{
    /// <summary>Nombre único de la categoría.</summary>
    public string Nombre { get; set; } = string.Empty;
}
