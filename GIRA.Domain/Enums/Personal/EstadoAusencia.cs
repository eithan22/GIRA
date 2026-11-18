namespace GIRA.Domain.Enums.Personal;

/// <summary>Estado de una solicitud de ausencia (SRS 5.4).</summary>
public enum EstadoAusencia
{
    /// <summary>Pendiente de decisión del gerente.</summary>
    Solicitada,

    /// <summary>Aprobada: bloquea la asignación de turnos en esas fechas (RN-PER-03).</summary>
    Aprobada,

    /// <summary>Rechazada por el gerente.</summary>
    Rechazada
}
