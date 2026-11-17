namespace GIRA.Application.DTOs.Reservas;

/// <summary>
/// Filtro del listado de visitas sin reserva (GI-402). Sin <see cref="Fecha"/> se usa el día de hoy
/// (hora de Santo Domingo).
/// </summary>
public class ListarVisitasRequest
{
    /// <summary>
    /// Día a listar, EXACTAMENTE como aaaa-mm-dd. Va como texto y no como <c>DateOnly</c> a propósito: el binding de
    /// <c>DateOnly</c> acepta formatos ambiguos como 01/12/2026 y lo lee como mes/día (12 de enero), no como el 1 de
    /// diciembre que esperaría quien usa día/mes. No puede ser futuro ni de hace más de 365 días.
    /// </summary>
    public string? Fecha { get; init; }
}
