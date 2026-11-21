using GIRA.Domain.Base;

namespace GIRA.Domain.Entities.Inventario;

/// <summary>Lo que entró de un insumo perecedero, con su fecha de vencimiento (RF-INV-14).</summary>
public class LoteInsumo : BaseEntity
{
    /// <summary>Insumo del lote.</summary>
    public Guid InsumoId { get; set; }

    /// <summary>Cantidad que queda del lote. Las salidas gastan primero el lote que vence antes.</summary>
    public decimal Cantidad { get; set; }

    /// <summary>Costo unitario registrado en la entrada; null en lotes históricos sin costo.</summary>
    public decimal? CostoUnitario { get; set; }

    /// <summary>Día en que entró.</summary>
    public DateOnly FechaEntrada { get; set; }

    /// <summary>Día en que vence.</summary>
    public DateOnly FechaVencimiento { get; set; }
}
