using GIRA.Domain.Base;

namespace GIRA.Domain.Entities.Inventario;

/// <summary>Agrupa los platos del menú (entradas, platos fuertes, postres, bebidas).</summary>
public class CategoriaPlato : BaseEntity
{
    /// <summary>Nombre único de la categoría.</summary>
    public string Nombre { get; set; } = string.Empty;
}
