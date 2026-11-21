namespace GIRA.Domain.Base;

/// <summary>
/// Clase base de la que heredan todas las entidades persistentes de GIRA.
/// Centraliza el identificador, la auditoría de creación y modificación,
/// el borrado lógico (soft delete) y el token de concurrencia optimista.
/// </summary>
/// <remarks>
/// Es código de dominio puro: no conoce EF Core ni atributos de persistencia.
/// El mapeo (columna <c>rowversion</c>, filtro global de borrado lógico, índices)
/// se define con Fluent API en GIRA.Infrastructure, y las propiedades de auditoría
/// las rellena un interceptor de <c>SaveChanges</c>; los casos de uso no las asignan a mano.
/// </remarks>
public abstract class BaseEntity
{
    /// <summary>Identificador único de la entidad (GUID secuencial generado por EF Core al insertar).</summary>
    public Guid Id { get; set; }

    /// <summary>Fecha y hora, en UTC, en que se creó el registro.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Usuario que creó el registro; null si la creación no vino de un usuario autenticado (seeder, procesos en segundo plano, clientes sin cuenta).</summary>
    public Guid? CreatedBy { get; set; }

    /// <summary>Fecha y hora, en UTC, de la última modificación; null si nunca se modificó.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Usuario que realizó la última modificación; null si nunca se modificó o no vino de un usuario autenticado.</summary>
    public Guid? UpdatedBy { get; set; }

    /// <summary>Indica si el registro está eliminado lógicamente. Los registros marcados quedan fuera de las consultas por el filtro global.</summary>
    public bool IsDeleted { get; set; }

    /// <summary>Fecha y hora, en UTC, del borrado lógico; null si el registro está activo.</summary>
    public DateTime? DeletedAt { get; set; }

    /// <summary>Usuario que ejecutó el borrado lógico; null si el registro está activo o el borrado lo hizo el sistema.</summary>
    public Guid? DeletedBy { get; set; }

    /// <summary>Token de concurrencia optimista, mapeado a columna <c>rowversion</c> de SQL Server.</summary>
    public byte[]? RowVersion { get; set; }
}
