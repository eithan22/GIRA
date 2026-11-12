namespace GIRA.Domain.Constants;

/// <summary>
/// Nombres de los roles fijos del sistema. Ningún módulo debe escribir
/// el nombre de un rol como texto suelto: siempre se referencia desde aquí.
/// </summary>
public static class Roles
{
    /// <summary>Acceso total al sistema.</summary>
    public const string Administrador = "Administrador";

    /// <summary>Gestiona el restaurante a nivel operativo y de negocio.</summary>
    public const string Gerente = "Gerente";

    /// <summary>Supervisa el turno y al personal en piso.</summary>
    public const string Supervisor = "Supervisor";

    /// <summary>Atiende mesas y toma órdenes.</summary>
    public const string Mesero = "Mesero";

    /// <summary>Prepara los pedidos en cocina.</summary>
    public const string Cocina = "Cocina";

    /// <summary>Todos los roles fijos del sistema.</summary>
    public static readonly string[] Todos =
    {
        Administrador,
        Gerente,
        Supervisor,
        Mesero,
        Cocina
    };
}
