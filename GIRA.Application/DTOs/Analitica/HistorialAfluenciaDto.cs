namespace GIRA.Application.DTOs.Analitica;

/// <summary>Asistencia y reservas no asistidas registradas para una fecha y franja.</summary>
public sealed record HistorialAfluenciaDto(
    DateOnly Fecha,
    Guid FranjaHorariaId,
    int ComensalesConReserva,
    int ComensalesSinReserva,
    int TotalComensales,
    int ReservasCompletadas,
    int ReservasNoAsistidas);
