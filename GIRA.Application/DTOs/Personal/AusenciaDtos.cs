using GIRA.Domain.Enums.Personal;

namespace GIRA.Application.DTOs.Personal;

/// <summary>Ausencia tal como se entrega al cliente.</summary>
public sealed record AusenciaDto(
    Guid Id,
    Guid EmpleadoId,
    DateOnly FechaDesde,
    DateOnly FechaHasta,
    TipoAusencia Tipo,
    string? Motivo,
    EstadoAusencia Estado,
    Guid? AprobadaPor);

/// <summary>Datos para registrar una solicitud de ausencia.</summary>
public sealed record AusenciaRequest(
    Guid EmpleadoId,
    DateOnly FechaDesde,
    DateOnly FechaHasta,
    TipoAusencia Tipo,
    string? Motivo);

/// <summary>Decisión sobre una solicitud de ausencia.</summary>
/// <param name="Estado">Aprobada o Rechazada.</param>
public sealed record CambiarEstadoAusenciaRequest(EstadoAusencia Estado);
