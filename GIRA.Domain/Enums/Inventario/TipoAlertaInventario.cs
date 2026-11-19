namespace GIRA.Domain.Enums.Inventario;

/// <summary>Tipos de alerta de inventario. Se guardan como número: no cambiarlos sin migración.</summary>
public enum TipoAlertaInventario
{
    /// <summary>El stock está en el punto de reorden o por debajo.</summary>
    StockBajo = 0,

    /// <summary>Con el consumo proyectado bajará del punto de reorden en los próximos días (RN-INV-07).</summary>
    QuiebreProyectado = 1,

    /// <summary>Un lote vence pronto (RN-INV-11).</summary>
    Vencimiento = 2,

    /// <summary>Una salida teórica pedía más de lo que había (RN-INV-02).</summary>
    Inconsistencia = 3
}
