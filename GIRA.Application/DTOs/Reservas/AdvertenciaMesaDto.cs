namespace GIRA.Application.DTOs.Reservas;

/// <summary>
/// Filtros de <c>GET /api/visitas/advertencia-mesa</c> (aviso de próxima reserva al registrar una visita sin
/// reserva). Llegan como texto para que un formato inválido produzca un 400 con mensaje en español del módulo.
/// </summary>
public class ConsultarAdvertenciaMesaRequest
{
    /// <summary>Id de la mesa; obligatorio.</summary>
    public string? MesaId { get; init; }

    /// <summary>Id de la franja en la que se registraría la visita; obligatorio.</summary>
    public string? FranjaId { get; init; }

    /// <summary>Fecha en formato aaaa-mm-dd; si no se envía, hoy (hora de Santo Domingo).</summary>
    public string? Fecha { get; init; }
}

/// <summary>
/// Resultado de la advertencia: si la mesa tiene una reserva vigente (Confirmada o EnCurso) en una franja
/// posterior del mismo día que todavía no ha terminado, con los datos mínimos para que el personal decida —
/// sin nombre ni teléfono del cliente. <see cref="MinutosFaltantes"/> es null si la fecha consultada no es hoy
/// (solo tiene sentido contar minutos desde "ahora" para el día de hoy).
/// </summary>
public record AdvertenciaMesaDto(
    bool HayAviso,
    string? Mensaje,
    string? FranjaNombre,
    TimeOnly? HoraInicio,
    int? Personas,
    int? MinutosFaltantes);
