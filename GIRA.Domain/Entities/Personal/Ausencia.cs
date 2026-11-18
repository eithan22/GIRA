using GIRA.Domain.Base;
using GIRA.Domain.Enums.Personal;

namespace GIRA.Domain.Entities.Personal;

/// <summary>
/// Solicitud de ausencia de un empleado entre dos fechas (SRS 5.4, RF-PER-11). Se crea aquí porque
/// la regla RN-PER-03 necesita saber si hay una ausencia aprobada; sus endpoints van en GI-305.
/// </summary>
public class Ausencia : BaseEntity
{
    /// <summary>Empleado que pide la ausencia.</summary>
    public Guid EmpleadoId { get; set; }

    /// <summary>Primer día de la ausencia.</summary>
    public DateOnly FechaDesde { get; set; }

    /// <summary>Último día de la ausencia; no anterior a <see cref="FechaDesde"/>.</summary>
    public DateOnly FechaHasta { get; set; }

    /// <summary>Tipo de ausencia.</summary>
    public TipoAusencia Tipo { get; set; }

    /// <summary>Motivo opcional.</summary>
    public string? Motivo { get; set; }

    /// <summary>Estado de la solicitud.</summary>
    public EstadoAusencia Estado { get; set; } = EstadoAusencia.Solicitada;

    /// <summary>Usuario que aprobó o rechazó la solicitud; null mientras esté pendiente.</summary>
    public Guid? AprobadaPor { get; set; }
}
