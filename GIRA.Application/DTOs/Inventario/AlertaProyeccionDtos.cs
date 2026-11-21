namespace GIRA.Application.DTOs.Inventario;

/// <summary>
/// Alerta de inventario. <see cref="AtendidaPor"/> es el nombre de quien la cerró, o "Sistema"
/// si se cerró sola con una entrada (RN-INV-09); null mientras está abierta.
/// </summary>
public record AlertaInventarioDto(
    Guid Id,
    Guid InsumoId,
    string Insumo,
    string Unidad,
    decimal StockInsumo,
    decimal PuntoReorden,
    string Tipo,
    string Estado,
    decimal StockActual,
    decimal? StockProyectado,
    DateOnly? FechaQuiebreEstimada,
    string? Detalle,
    DateTime FechaGeneracion,
    string? AtendidaPor,
    DateTime? FechaAtencion);

/// <summary>Filtro de alertas: Abierta, Atendida, Descartada o vacío para todas.</summary>
public class ListarAlertasRequest
{
    /// <summary>Estado a mostrar; vacío = todas.</summary>
    public string? Estado { get; init; }

    /// <summary>Página, desde 1.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Tamaño de página, de 1 a 100.</summary>
    public int PageSize { get; init; } = 20;
}

/// <summary>Atender o descartar una alerta: Estado = "Atendida" o "Descartada".</summary>
public record CambiarEstadoAlertaRequest(string Estado);

/// <summary>Rango de la proyección (por defecto, hoy y los 6 días siguientes; máximo 14 días desde hoy).</summary>
public class ProyeccionRequest
{
    /// <summary>Primer día (hoy o después).</summary>
    public DateOnly? Desde { get; init; }

    /// <summary>Último día.</summary>
    public DateOnly? Hasta { get; init; }
}

/// <summary>
/// Comensales de un día: con reserva, predicción de Analítica (null si no hay) y los usados en el cálculo
/// (el mayor de los dos).
/// </summary>
public record ComensalesDiaDto(DateOnly Fecha, int ConReserva, int? Prediccion, int Usados);

/// <summary>Consumo esperado de un insumo en un día y lo que quedaría al final de ese día.</summary>
public record ConsumoDiaDto(DateOnly Fecha, decimal Cantidad, decimal Restante);

/// <summary>Proyección de un insumo: consumo por día y los primeros días bajo el punto de reorden y sin stock.</summary>
public record ProyeccionInsumoDto(
    Guid InsumoId,
    string Insumo,
    string Unidad,
    decimal Stock,
    decimal PuntoReorden,
    IReadOnlyList<ConsumoDiaDto> Dias,
    DateOnly? FechaBajoReorden,
    DateOnly? FechaAgotamiento);

/// <summary>Proyección de consumo (RF-INV-12): comensales por día y consumo por insumo usado en recetas.</summary>
public record ProyeccionConsumoDto(IReadOnlyList<ComensalesDiaDto> Dias, IReadOnlyList<ProyeccionInsumoDto> Insumos);
