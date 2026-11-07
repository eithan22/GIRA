namespace GIRA.Application.Interfaces.Services.Reservas;

/// <summary>
/// Lectura de los parámetros del restaurante que necesita el módulo Reservas
/// (horario, días cerrados y políticas de tiempo). Ningún servicio de Reservas debe
/// escribir estos valores directamente en el código: siempre se leen de aquí.
/// </summary>
/// <remarks>
/// La implementación inicial (<c>ParametrosReservaService</c>) usa los valores fijos
/// del SRS. Cuando el módulo Configuración los exponga de verdad, solo se cambia el
/// registro de esta interfaz en el contenedor de dependencias.
/// </remarks>
public interface IParametrosReservaService
{
    /// <summary>Hora en la que el restaurante abre.</summary>
    TimeOnly HoraApertura { get; }

    /// <summary>Hora en la que el restaurante cierra.</summary>
    TimeOnly HoraCierre { get; }

    /// <summary>Días de la semana en los que el restaurante no abre.</summary>
    IReadOnlySet<DayOfWeek> DiasCerrados { get; }

    /// <summary>Antelación mínima con la que se puede crear una reserva.</summary>
    TimeSpan AntelacionMinima { get; }

    /// <summary>Antelación máxima con la que se puede crear una reserva.</summary>
    TimeSpan AntelacionMaxima { get; }

    /// <summary>Tiempo antes de la reserva hasta el cual se puede cancelar.</summary>
    TimeSpan LimiteCancelacion { get; }

    /// <summary>Tiempo antes de la franja desde el cual se permite hacer check-in.</summary>
    TimeSpan VentanaCheckIn { get; }

    /// <summary>Tiempo de tolerancia tras el cual una reserva Confirmada se marca NoAsistio.</summary>
    TimeSpan ToleranciaNoShow { get; }
}
