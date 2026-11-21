namespace GIRA.Domain.Enums.Reservas;

/// <summary>
/// Estados posibles de una reserva. No hay historial de estados: la reserva
/// guarda únicamente su estado actual y, si fue cancelada, el motivo.
/// </summary>
/// <remarks>
/// El índice único filtrado de Reserva usa [Estado] IN (0, 1) (Confirmada y EnCurso);
/// no cambies estos números sin crear una migración nueva.
/// </remarks>
public enum EstadoReserva
{
    /// <summary>Reserva creada y vigente, pendiente de que el cliente llegue.</summary>
    Confirmada = 0,

    /// <summary>El cliente hizo check-in y está siendo atendido.</summary>
    EnCurso = 1,

    /// <summary>La visita terminó con normalidad.</summary>
    Completada = 2,

    /// <summary>El cliente o el personal canceló la reserva antes de la visita.</summary>
    Cancelada = 3,

    /// <summary>El cliente no llegó dentro de la tolerancia configurada.</summary>
    NoAsistio = 4
}
