using GIRA.Domain.Base;

namespace GIRA.Domain.Entities.Inventario;

/// <summary>A quién se le compra un insumo (RF-INV-13). Desde el SRS v1.4 no lleva correo.</summary>
public class Proveedor : BaseEntity
{
    /// <summary>Nombre único del proveedor.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Teléfono dominicano normalizado: 10 dígitos que empiezan por 809, 829 u 849.</summary>
    public string Telefono { get; set; } = string.Empty;

    /// <summary>Un proveedor inactivo no se puede asignar a un insumo.</summary>
    public bool Activo { get; set; } = true;
}
