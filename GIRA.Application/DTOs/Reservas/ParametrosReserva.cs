namespace GIRA.Application.DTOs.Reservas;

/// <summary>Horario de operación de un día de la semana.</summary>
/// <param name="Abierto">Si el restaurante abre ese día de la semana.</param>
/// <param name="HoraApertura">Hora de apertura, en hora local.</param>
/// <param name="HoraCierre">Hora de cierre, en hora local.</param>
public sealed record HorarioDia(bool Abierto, TimeOnly HoraApertura, TimeOnly HoraCierre);

/// <summary>
/// Instantánea inmutable de las políticas de reserva y del horario del restaurante (GI-226). Los servicios
/// la obtienen con <c>IParametrosReservaService.ObtenerAsync</c>; nunca se modifica: un cambio de
/// configuración produce una instantánea nueva.
/// </summary>
/// <param name="AntelacionMinima">Antelación mínima para crear una reserva (solo reservas nuevas).</param>
/// <param name="AntelacionMaxima">Antelación máxima para crear una reserva (solo reservas nuevas).</param>
/// <param name="LimiteCancelacion">Tiempo antes del inicio de la franja hasta el cual el cliente puede cancelar.</param>
/// <param name="VentanaCheckIn">Tiempo antes del inicio de la franja desde el cual se permite el check-in.</param>
/// <param name="MaxReservasVigentesPorTelefono">Máximo de reservas vigentes con un mismo teléfono.</param>
/// <param name="MaxPersonasPorReserva">Tamaño máximo de grupo configurado (el efectivo también depende de las mesas).</param>
/// <param name="Horarios">Horario de cada día de la semana.</param>
/// <param name="DiasCerrados">Fechas concretas en las que el restaurante está cerrado.</param>
public sealed record ParametrosReserva(
    TimeSpan AntelacionMinima,
    TimeSpan AntelacionMaxima,
    TimeSpan LimiteCancelacion,
    TimeSpan VentanaCheckIn,
    int MaxReservasVigentesPorTelefono,
    int MaxPersonasPorReserva,
    IReadOnlyDictionary<DayOfWeek, HorarioDia> Horarios,
    IReadOnlySet<DateOnly> DiasCerrados)
{
    /// <summary>
    /// True si el restaurante abre esa fecha: su día de la semana está abierto y la fecha no es un día
    /// cerrado. Un día de la semana sin horario se considera cerrado.
    /// </summary>
    public bool EstaAbierto(DateOnly fecha) =>
        Horarios.TryGetValue(fecha.DayOfWeek, out var horario) && horario.Abierto && !DiasCerrados.Contains(fecha);

    /// <summary>Horario de esa fecha, o null si el restaurante está cerrado ese día.</summary>
    public HorarioDia? HorarioDelDia(DateOnly fecha) =>
        EstaAbierto(fecha) ? Horarios[fecha.DayOfWeek] : null;
}
