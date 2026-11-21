using GIRA.Domain.Base;

namespace GIRA.Domain.Entities.Inventario;

/// <summary>Plato del menú. Su receta y su porcentaje de demanda alimentan la proyección.</summary>
public class Plato : BaseEntity
{
    /// <summary>Nombre único del plato.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Categoría del plato.</summary>
    public Guid CategoriaPlatoId { get; set; }

    /// <summary>Categoría cargada por EF Core.</summary>
    public CategoriaPlato? CategoriaPlato { get; set; }

    /// <summary>Precio de venta en RD$.</summary>
    public decimal Precio { get; set; }

    /// <summary>Parte estimada de los comensales que pide el plato, de 0 a 100.</summary>
    public decimal PorcentajeDemanda { get; set; }

    /// <summary>Solo los platos activos con receta entran en la proyección (RN-INV-04).</summary>
    public bool Activo { get; set; } = true;

    /// <summary>Receta del plato; null si todavía no tiene.</summary>
    public Receta? Receta { get; set; }
}
