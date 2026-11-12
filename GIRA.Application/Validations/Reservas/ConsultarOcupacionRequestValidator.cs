using System.Globalization;
using FluentValidation;
using GIRA.Application.DTOs.Reservas;

namespace GIRA.Application.Validations.Reservas;

/// <summary>Valida los filtros del mapa de ocupación (GI-167): franja obligatoria y fecha en formato aaaa-mm-dd.</summary>
public class ConsultarOcupacionRequestValidator : AbstractValidator<ConsultarOcupacionRequest>
{
    /// <summary>Convierte el texto a fecha; solo acepta el formato aaaa-mm-dd.</summary>
    public static bool TryParseFecha(string? texto, out DateOnly fecha) =>
        DateOnly.TryParseExact(texto?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha);

    /// <summary>Convierte el texto a id de franja; no acepta el id vacío.</summary>
    public static bool TryParseFranja(string? texto, out Guid franjaId) =>
        Guid.TryParse(texto?.Trim(), out franjaId) && franjaId != Guid.Empty;

    /// <summary>Define las reglas de franja y fecha.</summary>
    public ConsultarOcupacionRequestValidator()
    {
        RuleFor(r => r.FranjaId)
            .Cascade(CascadeMode.Stop)
            .Must(f => !string.IsNullOrWhiteSpace(f)).WithMessage("La franja es obligatoria.")
            .Must(f => TryParseFranja(f, out _)).WithMessage("La franja indicada no es válida.");

        RuleFor(r => r.Fecha)
            .Must(f => TryParseFecha(f, out _))
            .When(r => !string.IsNullOrWhiteSpace(r.Fecha))
            .WithMessage("La fecha no es válida; usa el formato aaaa-mm-dd.");
    }
}
