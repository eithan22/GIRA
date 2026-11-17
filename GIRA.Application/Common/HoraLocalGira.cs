namespace GIRA.Application.Common;

/// <summary>
/// Hora local del restaurante (America/Santo_Domingo), calculada a partir de un
/// <see cref="TimeProvider"/> inyectado. Evita repetir <c>TimeZoneInfo.FindSystemTimeZoneById</c>
/// en cada clase que necesite "ahora" o "hoy" en hora local.
/// </summary>
public static class HoraLocalGira
{
    /// <summary>Zona horaria del restaurante.</summary>
    public static readonly TimeZoneInfo ZonaHoraria = TimeZoneInfo.FindSystemTimeZoneById("America/Santo_Domingo");

    /// <summary>Fecha y hora actuales, en hora local de Santo Domingo.</summary>
    public static DateTime AhoraLocal(this TimeProvider timeProvider) =>
        TimeZoneInfo.ConvertTimeFromUtc(timeProvider.GetUtcNow().UtcDateTime, ZonaHoraria);

    /// <summary>Fecha actual (sin hora), en hora local de Santo Domingo.</summary>
    public static DateOnly HoyLocal(this TimeProvider timeProvider) =>
        DateOnly.FromDateTime(timeProvider.AhoraLocal());
}
