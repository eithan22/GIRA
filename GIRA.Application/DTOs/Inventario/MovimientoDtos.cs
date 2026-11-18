using GIRA.Application.Common;

namespace GIRA.Application.DTOs.Inventario;

/// <summary>
/// Entrada, merma o ajuste. <see cref="Cantidad"/> es positiva en entradas y mermas; en un ajuste
/// el signo dice si suma (+) o resta (−). <see cref="FechaVencimiento"/> es obligatoria en la entrada
/// de un insumo perecedero.
/// </summary>
public record RegistrarMovimientoRequest(
    Guid InsumoId,
    string Tipo,
    decimal Cantidad,
    decimal? CostoUnitario,
    string? Motivo,
    string? Referencia,
    DateOnly? FechaVencimiento);

/// <summary>Movimiento del kardex con el saldo después de aplicarlo. <see cref="Cantidad"/> lleva signo.</summary>
public record MovimientoDto(
    Guid Id,
    Guid InsumoId,
    string Tipo,
    decimal Cantidad,
    decimal? CostoUnitario,
    DateTime Fecha,
    string? Motivo,
    string? Referencia,
    bool Teorico,
    string? Usuario,
    decimal SaldoResultante);

/// <summary>Kardex de un insumo entre dos fechas locales (por defecto, los últimos 30 días).</summary>
public class KardexRequest
{
    /// <summary>Insumo; obligatorio.</summary>
    public Guid? InsumoId { get; init; }

    /// <summary>Primer día incluido (hora local).</summary>
    public DateOnly? Desde { get; init; }

    /// <summary>Último día incluido (hora local).</summary>
    public DateOnly? Hasta { get; init; }

    /// <summary>Página, desde 1.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Tamaño de página, de 1 a 200.</summary>
    public int PageSize { get; init; } = 50;
}

/// <summary>Kardex: saldo al inicio, totales por tipo en el período y los movimientos en orden.</summary>
public record KardexDto(
    Guid InsumoId,
    string Insumo,
    string Unidad,
    decimal StockActual,
    DateOnly Desde,
    DateOnly Hasta,
    decimal SaldoInicial,
    decimal Entradas,
    decimal Salidas,
    decimal Mermas,
    decimal Ajustes,
    PagedResult<MovimientoDto> Movimientos);

/// <summary>Conteo físico: lo contado de cada insumo y un motivo común para los ajustes.</summary>
public record ConteoFisicoRequest(string Motivo, IReadOnlyList<LineaConteoRequest> Lineas);

/// <summary>Cantidad contada de un insumo.</summary>
public record LineaConteoRequest(Guid InsumoId, decimal CantidadContada);

/// <summary>Ajustes que generó el conteo (uno por cada diferencia).</summary>
public record ConteoFisicoResultadoDto(int AjustesGenerados, IReadOnlyList<MovimientoDto> Ajustes);
