using System.Globalization;
using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;

namespace GIRA.Application.Validations.Reservas;

/// <summary>
/// Valida la fecha del listado de visitas sin reserva (GI-402): formato aaaa-mm-dd estricto, y que no sea futura ni
/// de hace más de un año.
/// </summary>
public class ListarVisitasRequestValidator : AbstractValidator<ListarVisitasRequest>
{
    /// <summary>Días hacia atrás que se permiten consultar.</summary>
    public const int DiasMaximosAtras = 365;

    /// <summary>Define las reglas de formato y rango de la fecha, con el día de hoy de Santo Domingo.</summary>
    public ListarVisitasRequestValidator(TimeProvider reloj)
    {
        RuleFor(r => r.Fecha)
            .Cascade(CascadeMode.Stop)
            .Must(texto => TryParseFecha(texto, out _))
            .WithMessage("La fecha debe tener el formato aaaa-mm-dd.")
            .Must(texto => TryParseFecha(texto, out var fecha) && fecha <= reloj.HoyLocal())
            .WithMessage("La fecha no puede ser futura.")
            .Must(texto => TryParseFecha(texto, out var fecha) && fecha >= reloj.HoyLocal().AddDays(-DiasMaximosAtras))
            .WithMessage($"La fecha no puede ser de hace más de {DiasMaximosAtras} días.")
            .When(r => !string.IsNullOrWhiteSpace(r.Fecha));
    }

    /// <summary>Interpreta el texto como aaaa-mm-dd exacto (con ceros: 2026-12-01), sin depender de la cultura.</summary>
    public static bool TryParseFecha(string? texto, out DateOnly fecha) =>
        DateOnly.TryParseExact(texto?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha);
}
