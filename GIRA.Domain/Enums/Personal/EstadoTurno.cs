namespace GIRA.Domain.Enums.Personal;

/// <summary>Estado de un turno asignado a un empleado (SRS 5.4).</summary>
public enum EstadoTurno
{
    /// <summary>Asignado y pendiente de confirmar.</summary>
    Programado,

    /// <summary>Confirmado por el empleado o el supervisor.</summary>
    Confirmado,

    /// <summary>El empleado cumplió el turno.</summary>
    Completado,

    /// <summary>El empleado faltó al turno.</summary>
    Ausente,

    /// <summary>Anulado; no cuenta para cobertura ni horas.</summary>
    Cancelado
}
