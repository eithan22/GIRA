namespace GIRA.Application.DTOs.Inventario;

/// <summary>Unidad de medida (kg, g, l, ml, unidad).</summary>
public record UnidadMedidaDto(Guid Id, string Nombre, string Abreviatura);

/// <summary>Alta o edición de una unidad de medida.</summary>
public record GuardarUnidadMedidaRequest(string Nombre, string Abreviatura);

/// <summary>Categoría de insumo o de plato.</summary>
public record CategoriaDto(Guid Id, string Nombre);

/// <summary>Alta o edición de una categoría de insumo o de plato.</summary>
public record GuardarCategoriaRequest(string Nombre);

/// <summary>Proveedor; el teléfono va normalizado (solo dígitos).</summary>
public record ProveedorDto(Guid Id, string Nombre, string Telefono, bool Activo);

/// <summary>Alta o edición de un proveedor. El teléfono acepta espacios, guiones y paréntesis.</summary>
public record GuardarProveedorRequest(string Nombre, string Telefono, bool Activo);
