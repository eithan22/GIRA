namespace GIRA.Application.Common;

/// <summary>Una reserva que el proceso automático no pudo procesar (se reintenta en el ciclo siguiente).</summary>
/// <param name="ReservaId">Id de la reserva.</param>
/// <param name="Excepcion">Error ocurrido; el llamador lo registra (no contiene datos personales).</param>
public sealed record FalloProcesoReserva(Guid ReservaId, Exception Excepcion);

/// <summary>Resultado de un ciclo del proceso automático de reservas (GI-161 y GI-224).</summary>
/// <param name="NoAsistidas">Reservas marcadas NoAsistio en este ciclo (cada una con su evento).</param>
/// <param name="Omitidas">Reservas que otra ejecución simultánea ya había modificado (RowVersion): no es un error.</param>
/// <param name="Fallos">Reservas que fallaron por otro motivo; el resto del ciclo continuó.</param>
/// <param name="TopeAlcanzado">True si algún paso encontró al menos <paramref name="Tope"/> reservas vencidas; el resto queda para el ciclo siguiente.</param>
/// <param name="Tope">Máximo de reservas por paso y por ciclo.</param>
/// <param name="Completadas">Reservas EnCurso completadas al terminar su franja (GI-224), cada una con su evento.</param>
public sealed record ResultadoProcesoReservas(
    int NoAsistidas,
    int Omitidas,
    IReadOnlyList<FalloProcesoReserva> Fallos,
    bool TopeAlcanzado,
    int Tope,
    int Completadas = 0)
{
    /// <summary>Ciclo sin nada que hacer.</summary>
    public static ResultadoProcesoReservas Vacio => new(0, 0, [], false, 0);

    /// <summary>Suma los resultados de dos pasos del mismo ciclo.</summary>
    public ResultadoProcesoReservas Combinar(ResultadoProcesoReservas otro) => new(
        NoAsistidas + otro.NoAsistidas,
        Omitidas + otro.Omitidas,
        [.. Fallos, .. otro.Fallos],
        TopeAlcanzado || otro.TopeAlcanzado,
        Math.Max(Tope, otro.Tope),
        Completadas + otro.Completadas);
}
