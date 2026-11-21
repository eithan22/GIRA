namespace GIRA.Application.DTOs.Inventario;

/// <summary>Insumo con su stock y su nivel (Normal, Bajo o Critico).</summary>
public record InsumoDto(
    Guid Id,
    string Nombre,
    Guid CategoriaInsumoId,
    string Categoria,
    Guid UnidadMedidaId,
    string Unidad,
    decimal CantidadStock,
    decimal PuntoReorden,
    string Nivel,
    decimal CostoUnitario,
    bool Perecedero,
    Guid? ProveedorId,
    string? Proveedor,
    bool Activo);

/// <summary>Alta o edición de un insumo. El stock no se edita aquí: solo cambia con movimientos (RN-INV-01).</summary>
public record GuardarInsumoRequest(
    string Nombre,
    Guid CategoriaInsumoId,
    Guid UnidadMedidaId,
    decimal PuntoReorden,
    decimal CostoUnitario,
    bool Perecedero,
    Guid? ProveedorId);

/// <summary>Activar o desactivar un insumo.</summary>
public record CambiarEstadoInsumoRequest(bool Activo);

/// <summary>Filtros del catálogo de insumos (page/pageSize, máximo 200).</summary>
public class ListarInsumosRequest
{
    /// <summary>Texto que debe aparecer en el nombre.</summary>
    public string? Buscar { get; init; }

    /// <summary>Solo esta categoría.</summary>
    public Guid? Categoria { get; init; }

    /// <summary>Normal, Bajo o Critico. Solo aplica a insumos activos.</summary>
    public string? Nivel { get; init; }

    /// <summary>Si es false (por defecto), solo se listan los activos.</summary>
    public bool IncluirInactivos { get; init; }

    /// <summary>Página, desde 1.</summary>
    public int Page { get; init; } = 1;

    /// <summary>Tamaño de página, de 1 a 200.</summary>
    public int PageSize { get; init; } = 20;
}

/// <summary>Cuántos insumos activos hay y cuántos están en nivel bajo o crítico.</summary>
public record ResumenInsumosDto(int Activos, int Bajos, int Criticos);
