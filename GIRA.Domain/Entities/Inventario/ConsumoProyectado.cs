using GIRA.Domain.Base;

namespace GIRA.Domain.Entities.Inventario;

/// <summary>Consumo esperado de un insumo en una fecha. Se recalcula; único por fecha e insumo.</summary>
public class ConsumoProyectado : BaseEntity
{
    /// <summary>Fecha proyectada (hora local del restaurante).</summary>
    public DateOnly Fecha { get; set; }

    /// <summary>Insumo proyectado.</summary>
    public Guid InsumoId { get; set; }

    /// <summary>Cantidad esperada, en la unidad del insumo.</summary>
    public decimal CantidadEsperada { get; set; }

    /// <summary>Comensales usados en el cálculo.</summary>
    public int ComensalesBase { get; set; }

    /// <summary>Cuándo se calculó, en UTC.</summary>
    public DateTime FechaCalculo { get; set; }
}
