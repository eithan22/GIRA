using GIRA.Application.DTOs.Reservas;

namespace GIRA.Application.Services.Reservas;

/// <summary>
/// Fuente única de los valores por defecto de la configuración de reservas: la usan el seeder, la
/// instantánea cuando todavía no hay configuración guardada y el «Restablecer valores recomendados» del
/// frontend. Ya no implementa <c>IParametrosReservaService</c> (lo hace <c>ParametrosReservaDbService</c>).
/// </summary>
public static class ParametrosReservaService
{
    /// <summary>Valores por defecto de las políticas y del horario.</summary>
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

    /// <summary>Horario por defecto: los 7 días abiertos, de 12:00 a 22:30.</summary>
    public static HorarioDia HorarioPorDefecto() =>
        new(true, ValoresPorDefecto.HoraApertura, ValoresPorDefecto.HoraCierre);

    /// <summary>Instantánea con todos los valores por defecto, sin días cerrados.</summary>
    public static ParametrosReserva PorDefecto() => new(
        TimeSpan.FromMinutes(ValoresPorDefecto.AntelacionMinimaMinutos),
        TimeSpan.FromDays(ValoresPorDefecto.AntelacionMaximaDias),
        TimeSpan.FromMinutes(ValoresPorDefecto.LimiteCancelacionMinutos),
        TimeSpan.FromMinutes(ValoresPorDefecto.VentanaCheckInMinutos),
        ValoresPorDefecto.MaxReservasVigentesPorTelefono,
        ValoresPorDefecto.MaxPersonasPorReserva,
        Enum.GetValues<DayOfWeek>().ToDictionary(dia => dia, _ => HorarioPorDefecto()),
        new HashSet<DateOnly>());
}
