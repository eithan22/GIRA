namespace GIRA.Application.DTOs.Analitica;

/// <summary>Recomendación vigente y su estado de revisión.</summary>
public sealed record RecomendacionDto(
    Guid Id, DateOnly Fecha, string Tipo, string Mensaje, string Prioridad,
    string Estado, string Origen, DateTimeOffset FechaGeneracion);

/// <summary>Estado al que se desea cambiar una recomendación.</summary>
public sealed record ActualizarRecomendacionRequest(string? Estado);
