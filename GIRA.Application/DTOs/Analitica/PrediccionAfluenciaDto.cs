namespace GIRA.Application.DTOs.Analitica;

/// <summary>Predicción vigente para una fecha y franja; los límites ausentes son desconocidos.</summary>
public sealed record PrediccionAfluenciaDto(
    DateOnly Fecha,
    Guid FranjaHorariaId,
    string Franja,
    int AfluenciaEstimada,
    decimal? LimiteInferior,
    decimal? LimiteSuperior,
    bool BajaConfianza,
    DateTimeOffset GeneradaEn,
    string VersionModelo);
