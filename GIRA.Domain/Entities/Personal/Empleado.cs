using GIRA.Domain.Base;

namespace GIRA.Domain.Entities.Personal;

/// <summary>
/// Persona que trabaja en el restaurante. Tiene un cargo y puede vincularse a una cuenta de usuario
/// (GI-133, en Seguridad). Los empleados no se eliminan: se activan o desactivan.
/// </summary>
public class Empleado : BaseEntity
{
    /// <summary>Nombre de pila.</summary>
    public string Nombre { get; set; } = string.Empty;

    /// <summary>Apellido.</summary>
    public string Apellido { get; set; } = string.Empty;

    /// <summary>Cédula de identidad; única entre empleados vigentes.</summary>
    public string Cedula { get; set; } = string.Empty;

    /// <summary>Teléfono de contacto, opcional.</summary>
    public string? Telefono { get; set; }

    /// <summary>Correo de contacto, opcional.</summary>
    public string? Email { get; set; }

    /// <summary>Cargo que ocupa el empleado.</summary>
    public Guid CargoId { get; set; }

    /// <summary>Fecha en que ingresó al restaurante.</summary>
    public DateOnly FechaIngreso { get; set; }

    /// <summary>Horas máximas que puede trabajar por semana (RN-PER-04).</summary>
    public int HorasMaximasSemana { get; set; }

    /// <summary>Indica si el empleado está vigente. Un empleado inactivo no se asigna a turnos.</summary>
    public bool Activo { get; set; } = true;
}
