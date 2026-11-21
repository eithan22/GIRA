using GIRA.Domain.Enums.Inventario;
using MediatR;

namespace GIRA.Application.Events.Inventario;

/// <summary>
/// Se publica, después de guardar, cada vez que Inventario abre una alerta (RF-INV-09). Lo pueden usar
/// Notificaciones o Analítica. Solo datos mínimos.
/// </summary>
public sealed record AlertaInventarioGeneradaEvent(
    Guid AlertaId,
    Guid InsumoId,
    TipoAlertaInventario Tipo,
    decimal StockActual,
    decimal? StockProyectado,
    DateOnly? FechaQuiebreEstimada) : INotification;
