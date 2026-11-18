using GIRA.Domain.Base;
using GIRA.Domain.Enums.Inventario;

namespace GIRA.Domain.Entities.Inventario;

/// <summary>Todo cambio de stock queda como un movimiento (RN-INV-01). Es el kardex.</summary>
public class MovimientoInventario : BaseEntity
{
    /// <summary>Insumo afectado.</summary>
    public Guid InsumoId { get; set; }

    /// <summary>Entrada, salida, merma o ajuste.</summary>
    public TipoMovimientoInventario Tipo { get; set; }

    /// <summary>Variación del stock con signo: positiva suma, negativa resta.</summary>
    public decimal Cantidad { get; set; }

    /// <summary>Costo por unidad de una entrada, en RD$; null en los demás tipos.</summary>
    public decimal? CostoUnitario { get; set; }

    /// <summary>Fecha y hora del movimiento, en UTC.</summary>
    public DateTime Fecha { get; set; }

    /// <summary>Motivo; obligatorio en mermas y ajustes (RN-INV-03).</summary>
    public string? Motivo { get; set; }

    /// <summary>Factura, conteo físico o visita que originó el movimiento.</summary>
    public string? Referencia { get; set; }

    /// <summary>True si es una salida estimada por una visita completada, no contada.</summary>
    public bool Teorico { get; set; }

    /// <summary>Quién lo registró; null si lo hizo el sistema.</summary>
    public Guid? UsuarioId { get; set; }

    /// <summary>Stock del insumo justo después de este movimiento (saldo del kardex, RF-INV-06).</summary>
    public decimal SaldoResultante { get; set; }
}
