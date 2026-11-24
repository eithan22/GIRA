namespace GIRA.Domain.Enums.Personal;

/// <summary>Estado de una sugerencia de personal (SRS 5.4, RF-PER-09 y RF-PER-10).</summary>
public enum EstadoSugerencia
{
    /// <summary>Esperando la decisión del gerente o del supervisor.</summary>
    Pendiente,

    /// <summary>Aceptada por quien la resolvió. No cambia turnos por sí sola (RN-PER-06).</summary>
    Aceptada,

    /// <summary>Rechazada por quien la resolvió.</summary>
    Rechazada,

    /// <summary>Reemplazada por sugerencias de una predicción más nueva de las mismas fechas (RN-PER-07).</summary>
    Vencida
}
