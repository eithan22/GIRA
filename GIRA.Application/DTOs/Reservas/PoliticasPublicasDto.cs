namespace GIRA.Application.DTOs.Reservas;

/// <summary>
/// Políticas de reserva que ve el cliente en /reservar (solo lectura, sin autenticación). Deliberadamente solo
/// lo necesario para mostrar textos, la ventana y a dónde llamar: el teléfono de contacto, la antelación, el
/// límite de cancelación, el tamaño de grupo y las fechas cerradas. No incluye la ventana de check-in, la
/// tolerancia de no-show, el máximo de reservas por teléfono, los horarios de operación ni datos de quién configuró.
/// </summary>
/// <param name="AntelacionMinimaMinutos">Antelación mínima para reservar, en minutos.</param>
/// <param name="AntelacionMaximaDias">Hasta cuántos días antes se puede reservar.</param>
/// <param name="LimiteCancelacionMinutos">Minutos antes del inicio de la franja hasta los que se puede cancelar.</param>
/// <param name="MaxPersonas">Tamaño máximo efectivo de grupo: el menor entre lo configurado y la mesa activa más grande.</param>
/// <param name="MaxReservasVigentesPorTelefono">Máximo de reservas vigentes con un mismo teléfono; las pantallas lo muestran en sus textos.</param>
/// <param name="TelefonoContacto">Teléfono del restaurante para grupos grandes y dudas; null si no se configuró.</param>
/// <param name="DiasCerrados">Fechas cerradas dentro de la ventana de reserva (de hoy hasta la antelación máxima).</param>
public sealed record PoliticasPublicasDto(
    int AntelacionMinimaMinutos,
    int AntelacionMaximaDias,
    int LimiteCancelacionMinutos,
    int MaxPersonas,
    int MaxReservasVigentesPorTelefono,
    string? TelefonoContacto,
    IReadOnlyList<DateOnly> DiasCerrados);
