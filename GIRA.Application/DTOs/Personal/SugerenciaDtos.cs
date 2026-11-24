using GIRA.Domain.Enums.Personal;

namespace GIRA.Application.DTOs.Personal;

/// <summary>Sugerencia de personal con los nombres de franja y cargo listos para mostrar (GI-304, GI-365).</summary>
public record SugerenciaDto(
    Guid Id,
    DateOnly Fecha,
    Guid FranjaHorariaId,
    string FranjaNombre,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    Guid CargoId,
    string CargoNombre,
    int PersonalPlanificado,
    int PersonalRequerido,
    int Diferencia,
    EstadoSugerencia Estado,
    DateTime FechaGeneracion,
    Guid? ResueltaPor);

/// <summary>Decisión sobre una sugerencia. Solo acepta Aceptada o Rechazada (RF-PER-10).</summary>
public record ResolverSugerenciaRequest(EstadoSugerencia Estado);
