using GIRA.Domain.Base;

namespace GIRA.Domain.Entities.Inventario;

/// <summary>Materia prima que se compra y se consume en las recetas.</summary>
public class Insumo : BaseEntity
{
    /// <summary>Nombre único del insumo.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Categoría del insumo.</summary>
    public Guid CategoriaInsumoId { get; set; }

    /// <summary>Categoría cargada por EF Core.</summary>
    public CategoriaInsumo? CategoriaInsumo { get; set; }

    /// <summary>Unidad en la que se expresan el stock y las cantidades de las recetas.</summary>
    public Guid UnidadMedidaId { get; set; }

    /// <summary>Unidad cargada por EF Core.</summary>
    public UnidadMedida? UnidadMedida { get; set; }

    /// <summary>Stock actual. Solo cambia con <see cref="AplicarMovimiento"/> (RN-INV-01).</summary>
    public decimal CantidadStock { get; private set; }

    /// <summary>Bajo este nivel hay que reabastecer.</summary>
    public decimal PuntoReorden { get; set; }

    /// <summary>Costo de referencia por unidad, en RD$.</summary>
    public decimal CostoUnitario { get; set; }

    /// <summary>Si es true, cada entrada guarda un lote con fecha de vencimiento.</summary>
    public bool Perecedero { get; set; }

    /// <summary>Proveedor preferido; opcional.</summary>
    public Guid? ProveedorId { get; set; }

    /// <summary>Proveedor cargado por EF Core.</summary>
    public Proveedor? Proveedor { get; set; }

    /// <summary>Un insumo inactivo no se usa en movimientos ni en recetas nuevas.</summary>
    public bool Activo { get; set; } = true;

    /// <summary>
    /// Suma <paramref name="variacion"/> al stock (negativa para salidas). Lanza
    /// <see cref="InvalidOperationException"/> si el stock quedaría por debajo de cero (RN-INV-02).
    /// </summary>
    public void AplicarMovimiento(decimal variacion)
    {
        var nuevo = CantidadStock + variacion;
        if (nuevo < 0)
        {
            throw new InvalidOperationException("El stock no puede quedar por debajo de cero.");
        }

        CantidadStock = nuevo;
    }
}
