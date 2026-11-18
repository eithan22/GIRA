using GIRA.Domain.Base;
using GIRA.Domain.Enums.Inventario;

namespace GIRA.Domain.Entities.Inventario;

/// <summary>Aviso de reabastecimiento, vencimiento o inconsistencia de un insumo.</summary>
public class AlertaInventario : BaseEntity
{
    /// <summary>Insumo de la alerta.</summary>
    public Guid InsumoId { get; set; }

    /// <summary>Insumo cargado por EF Core.</summary>
    public Insumo? Insumo { get; set; }

    /// <summary>Qué pasó.</summary>
    public TipoAlertaInventario Tipo { get; set; }

    /// <summary>Stock cuando se generó.</summary>
    public decimal StockActual { get; set; }

    /// <summary>Stock esperado al final de los días proyectados; null si no aplica.</summary>
    public decimal? StockProyectado { get; set; }

    /// <summary>Primer día en que se espera bajar del punto de reorden; null si no aplica.</summary>
    public DateOnly? FechaQuiebreEstimada { get; set; }

    /// <summary>Texto adicional (lote que vence, diferencia de una salida teórica).</summary>
    public string? Detalle { get; set; }

    /// <summary>Lote que vence (solo en alertas de vencimiento): ese lote avisa una sola vez, aunque la alerta se cierre.</summary>
    public Guid? LoteInsumoId { get; set; }

    /// <summary>Abierta, Atendida o Descartada. Cambia solo con <see cref="Cerrar"/>.</summary>
    public EstadoAlertaInventario Estado { get; private set; } = EstadoAlertaInventario.Abierta;

    /// <summary>Cuándo se generó, en UTC.</summary>
    public DateTime FechaGeneracion { get; set; }

    /// <summary>Quién la atendió o descartó; null si fue el sistema.</summary>
    public Guid? AtendidaPor { get; private set; }

    /// <summary>Cuándo se atendió o descartó, en UTC.</summary>
    public DateTime? FechaAtencion { get; private set; }

    /// <summary>
    /// Pasa la alerta a <paramref name="estado"/> (Atendida o Descartada) y registra quién y cuándo (RF-INV-10).
    /// Lanza <see cref="InvalidOperationException"/> si ya no está abierta.
    /// </summary>
    public void Cerrar(EstadoAlertaInventario estado, Guid? usuarioId, DateTime ahoraUtc)
    {
        if (Estado != EstadoAlertaInventario.Abierta)
        {
            throw new InvalidOperationException("La alerta ya no está abierta.");
        }

        if (estado == EstadoAlertaInventario.Abierta)
        {
            throw new ArgumentException("Una alerta se cierra como Atendida o Descartada.", nameof(estado));
        }

        Estado = estado;
        AtendidaPor = usuarioId;
        FechaAtencion = ahoraUtc;
    }
}
