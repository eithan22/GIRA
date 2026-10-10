namespace GIRA.Application.DTOs.Analitica;

/// <summary>Datos de afluencia observada expuestos por la API.</summary>
public sealed record HistorialAfluenciaDto(
    DateOnly Fecha,
    TimeOnly HoraInicio,
    int CantidadComensales);
