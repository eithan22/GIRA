using GIRA.Domain.Base;

namespace GIRA.Domain.Entities.Inventario;

/// <summary>Una línea de la receta: cuánto de un insumo lleva el total de porciones.</summary>
public class RecetaDetalle : BaseEntity
{
    /// <summary>Receta a la que pertenece.</summary>
    public Guid RecetaId { get; set; }

    /// <summary>Insumo usado.</summary>
    public Guid InsumoId { get; set; }

    /// <summary>Insumo cargado por EF Core.</summary>
    public Insumo? Insumo { get; set; }

    /// <summary>Cantidad en la unidad del insumo, para el total de porciones de la receta.</summary>
    public decimal Cantidad { get; set; }
}
