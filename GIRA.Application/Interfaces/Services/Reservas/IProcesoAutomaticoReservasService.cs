using GIRA.Application.Common;

namespace GIRA.Application.Interfaces.Services.Reservas;

/// <summary>
/// Lógica de los procesos automáticos de reservas: decide qué reservas procesar en cada ciclo. Un
/// solo <c>BackgroundService</c> (Infrastructure) la invoca cada minuto; así toda la lógica es
/// testeable sin temporizadores.
/// </summary>
public interface IProcesoAutomaticoReservasService
{
    /// <summary>Un ciclo completo del proceso: marca los no-shows (GI-161) y completa las visitas vencidas (GI-224).</summary>
    Task<ResultadoProcesoReservas> EjecutarAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Marca NoAsistio las reservas Confirmada cuya tolerancia ya venció (RN-RES-07): pasada la
    /// <c>ToleranciaNoShow</c> desde el inicio de su franja, hasta 200 por ciclo y las más antiguas
    /// primero. Cada reserva se procesa por separado y publica <c>ReservaNoAsistidaEvent</c> una sola
    /// vez tras guardar; una que falla no detiene a las demás y se reintenta en el ciclo siguiente.
    /// Es idempotente: una reserva ya NoAsistio no vuelve a procesarse.
    /// </summary>
    Task<ResultadoProcesoReservas> MarcarNoShowsAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Completa las reservas EnCurso cuya franja ya terminó (RN-RES-08, RF-RES-18): desde el instante
    /// en que termina la franja, hasta 200 por ciclo y las más antiguas primero. Cada reserva se procesa
    /// por separado y publica <c>VisitaCompletadaEvent</c> (con las personas reales y <c>ConReserva = true</c>)
    /// una sola vez tras guardar. Es idempotente: una reserva ya Completada (también si el personal la
    /// completó a mano) no vuelve a procesarse ni a publicar.
    /// </summary>
    Task<ResultadoProcesoReservas> CompletarVencidasAsync(CancellationToken cancellationToken);
}
