using GIRA.Application.Interfaces.Services.Reservas;

namespace GIRA.Application.Services.Reservas;

/// <summary>
/// Implementación inicial de <see cref="IParametrosReservaService"/> con los valores fijos
/// del SRS, mientras el módulo Configuración no exponga sus propios valores.
/// </summary>
/// <remarks>
/// Estos valores son PROVISIONALES hasta que el módulo Configuración (GI-226) los entregue
/// de verdad. El horario 12:00–22:30, abierto todos los días, es un valor de arranque
/// pensado para que el Gerente lo pueda editar más adelante desde Configuración.
/// </remarks>
public class ParametrosReservaService : IParametrosReservaService
{
    /// <inheritdoc />
    public TimeOnly HoraApertura => new(12, 0);

    /// <inheritdoc />
    public TimeOnly HoraCierre => new(22, 30);

    /// <inheritdoc />
    public IReadOnlySet<DayOfWeek> DiasCerrados => new HashSet<DayOfWeek>();

    /// <inheritdoc />
    public TimeSpan AntelacionMinima => TimeSpan.FromHours(2);

    /// <inheritdoc />
    public TimeSpan AntelacionMaxima => TimeSpan.FromDays(30);

    /// <inheritdoc />
    public TimeSpan LimiteCancelacion => TimeSpan.FromHours(2);

    /// <inheritdoc />
    public TimeSpan VentanaCheckIn => TimeSpan.FromMinutes(30);

    /// <inheritdoc />
    public TimeSpan ToleranciaNoShow => TimeSpan.FromMinutes(15);
}
