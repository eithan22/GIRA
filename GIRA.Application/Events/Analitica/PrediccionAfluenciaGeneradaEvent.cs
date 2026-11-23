using MediatR;

namespace GIRA.Application.Events.Analitica;

/// <summary>Afluencia estimada de una franja en una fecha (redondeada a entero y nunca negativa, RN-ANA-04).</summary>
public sealed record AfluenciaEstimadaFranja(
    DateOnly Fecha, Guid FranjaHorariaId, int AfluenciaEstimada,
    Guid? PrediccionId = null, string? VersionModelo = null);

/// <summary>
/// Analítica lo publica después de generar predicciones (RN-ANA-07), con el rango de fechas afectado y la
/// afluencia estimada por fecha y franja (SRS 3.4). Lo escuchan Inventario y Personal.
/// </summary>
/// <remarks>
/// Analítica publica la versión 1 con identificadores persistidos y restaurante configurado.
/// Los campos opcionales permiten que los consumidores existentes sigan compilando.
/// </remarks>
public sealed record PrediccionAfluenciaGeneradaEvent(
    DateOnly Desde,
    DateOnly Hasta,
    IReadOnlyList<AfluenciaEstimadaFranja> Predicciones,
    Guid? EventoId = null,
    int? VersionContrato = null,
    Guid? RestauranteId = null,
    DateTimeOffset? OcurridoEnUtc = null,
    string? TipoEvento = null) : INotification;
