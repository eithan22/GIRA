namespace GIRA.Application.DTOs.Reservas;

/// <summary>
/// Filtros del mapa de ocupación (GI-167). Llegan como texto para que un formato inválido produzca un 400
/// con mensaje en español del módulo, en vez del error genérico del enlazado de modelos.
/// </summary>
public class ConsultarOcupacionRequest
{
    /// <summary>Fecha del mapa en formato aaaa-mm-dd; si no se envía, hoy (hora de Santo Domingo).</summary>
    public string? Fecha { get; init; }

    /// <summary>Id de la franja; obligatorio.</summary>
    public string? FranjaId { get; init; }
}
