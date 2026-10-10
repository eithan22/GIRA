namespace GIRA.Application.DTOs.Analitica;

/// <summary>Resultado de afluencia pronosticada expuesto por la API.</summary>
public sealed record PrediccionAfluenciaDto(
    DateOnly Fecha,
    TimeOnly HoraInicio,
    int AfluenciaEstimada,
    DateTime FechaGeneracion,
    string Modelo);
