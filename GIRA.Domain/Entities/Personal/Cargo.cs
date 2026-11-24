using GIRA.Domain.Base;

namespace GIRA.Domain.Entities.Personal;

/// <summary>
/// Cargo que puede ocupar un empleado del restaurante, por ejemplo mesero, cocinero,
/// bartender o anfitrión. Clasifica al personal y sirve de base para calcular el
/// personal requerido por cargo.
/// </summary>
public class Cargo : BaseEntity
{
    /// <summary>Nombre del cargo. Debe ser único en el restaurante.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Descripción opcional de las funciones del cargo.</summary>
    public string? Descripcion { get; set; }
}
