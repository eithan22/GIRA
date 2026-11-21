using MediatR;

namespace GIRA.Application.Events.Analitica;

/// <summary>Afluencia estimada de una franja en una fecha (redondeada a entero y nunca negativa, RN-ANA-04).</summary>
public sealed record AfluenciaEstimadaFranja(DateOnly Fecha, Guid FranjaHorariaId, int AfluenciaEstimada);

/// <summary>
/// Analítica lo publica después de generar predicciones (RN-ANA-07), con el rango de fechas afectado y la
/// afluencia estimada por fecha y franja (SRS 3.4). Lo escuchan Inventario y Personal.
/// </summary>
/// <remarks>Contrato tomado del SRS 3.4; Analítica todavía no lo publica (GI-8).</remarks>
public sealed record PrediccionAfluenciaGeneradaEvent(
    DateOnly Desde,
    DateOnly Hasta,
    IReadOnlyList<AfluenciaEstimadaFranja> Predicciones) : INotification;
