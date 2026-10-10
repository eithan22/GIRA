namespace GIRA.Application.DTOs.Analitica;

/// <summary>Indicadores gerenciales calculados a partir del historial y las predicciones.</summary>
public sealed record KpisAnaliticaDto(
    DateOnly Desde,
    DateOnly Hasta,
    int ComensalesUltimos30Dias,
    decimal PromedioDiario,
    int? PrediccionManana,
    decimal? VariacionPorcentual,
    int DiasConActividad);
