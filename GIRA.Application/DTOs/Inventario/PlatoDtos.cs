namespace GIRA.Application.DTOs.Inventario;

/// <summary>Plato con su precio, su porcentaje de demanda y cuántas líneas tiene su receta (0 = sin receta).</summary>
public record PlatoDto(
    Guid Id,
    string Nombre,
    Guid CategoriaPlatoId,
    string Categoria,
    decimal Precio,
    decimal PorcentajeDemanda,
    bool Activo,
    int LineasReceta);

/// <summary>Alta o edición de un plato.</summary>
public record GuardarPlatoRequest(string Nombre, Guid CategoriaPlatoId, decimal Precio, decimal PorcentajeDemanda, bool Activo);

/// <summary>
/// Lista de platos con la suma del porcentaje de demanda de los activos. <see cref="Advertencia"/>
/// trae el aviso de RN-INV-05 cuando la suma sale de 95 %–105 %; null si está dentro.
/// </summary>
public record ListadoPlatosDto(IReadOnlyList<PlatoDto> Items, decimal SumaDemandaActivos, string? Advertencia);

/// <summary>Plato guardado, con la suma de demanda resultante y el aviso de RN-INV-05 si aplica.</summary>
public record PlatoGuardadoDto(PlatoDto Plato, decimal SumaDemandaActivos, string? Advertencia);

/// <summary>Una línea de la receta, con la cantidad por porción ya calculada.</summary>
public record RecetaLineaDto(Guid InsumoId, string Insumo, string Unidad, bool InsumoActivo, decimal Cantidad, decimal CantidadPorPorcion);

/// <summary>Receta de un plato. <see cref="Porciones"/> es null si el plato todavía no tiene receta.</summary>
public record RecetaDto(Guid PlatoId, string Plato, int? Porciones, string? Instrucciones, IReadOnlyList<RecetaLineaDto> Lineas);

/// <summary>Reemplaza la receta completa del plato.</summary>
public record GuardarRecetaRequest(int Porciones, string? Instrucciones, IReadOnlyList<LineaRecetaRequest> Lineas);

/// <summary>Insumo y cantidad (en la unidad del insumo, para el total de porciones).</summary>
public record LineaRecetaRequest(Guid InsumoId, decimal Cantidad);
