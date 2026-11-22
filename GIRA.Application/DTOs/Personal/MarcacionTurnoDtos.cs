using System.ComponentModel.DataAnnotations;

namespace GIRA.Application.DTOs.Personal;

/// <summary>Marcación del turno propio del usuario autenticado.</summary>
public sealed record MarcacionTurnoDto(
    Guid Id,
    DateTimeOffset InicioEn,
    DateTimeOffset? FinEn,
    int MinutosPausa,
    decimal? HorasTrabajadas);

/// <summary>Datos para cerrar el turno propio, descontando una pausa no negativa.</summary>
public sealed record FinalizarTurnoRequest
{
    [Range(0, 720)]
    public int MinutosPausa { get; init; }
}
