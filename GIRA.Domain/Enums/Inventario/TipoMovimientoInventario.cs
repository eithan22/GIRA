namespace GIRA.Domain.Enums.Inventario;

/// <summary>Tipos de movimiento del kardex. Se guardan como número: no cambiarlos sin migración.</summary>
public enum TipoMovimientoInventario
{
    /// <summary>Compra o recepción.</summary>
    Entrada = 0,

    /// <summary>Consumo; hoy solo la salida teórica de una visita completada.</summary>
    Salida = 1,

    /// <summary>Daño, vencimiento o pérdida.</summary>
    Merma = 2,

    /// <summary>Corrección del stock, por ejemplo tras un conteo físico.</summary>
    Ajuste = 3
}
