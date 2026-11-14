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
    /// <summary>
    /// Valores por defecto de la configuración de reservas: fuente única para el seeder y para el
    /// «Restablecer valores recomendados» del frontend.
    /// </summary>
    public static class ValoresPorDefecto
    {
        /// <summary>Antelación mínima: 2 horas.</summary>
        public const int AntelacionMinimaMinutos = 120;
        /// <summary>Antelación máxima: 30 días.</summary>
        public const int AntelacionMaximaDias = 30;
        /// <summary>Límite de cancelación del cliente: 2 horas antes del inicio de la franja.</summary>
        public const int LimiteCancelacionMinutos = 120;
        /// <summary>Ventana de check-in: 15 minutos antes del inicio de la franja.</summary>
        public const int VentanaCheckInMinutos = 15;
        /// <summary>Máximo de reservas vigentes por teléfono.</summary>
        public const int MaxReservasVigentesPorTelefono = 2;
        /// <summary>Tamaño máximo de grupo.</summary>
        public const int MaxPersonasPorReserva = 8;
        /// <summary>Apertura por defecto, todos los días.</summary>
        public static readonly TimeOnly HoraApertura = new(12, 0);
        /// <summary>Cierre por defecto, todos los días.</summary>
        public static readonly TimeOnly HoraCierre = new(22, 30);
    }

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
}
