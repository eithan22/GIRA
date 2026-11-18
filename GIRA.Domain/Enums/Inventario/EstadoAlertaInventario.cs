namespace GIRA.Domain.Enums.Inventario;

/// <summary>Estados de una alerta. Se guardan como número: no cambiarlos sin migración.</summary>
public enum EstadoAlertaInventario
{
    /// <summary>Pendiente de atender.</summary>
    Abierta = 0,

    /// <summary>Alguien la resolvió, o se cerró sola con una entrada (RN-INV-09).</summary>
    Atendida = 1,

    /// <summary>Alguien decidió que no aplica.</summary>
    Descartada = 2
}
