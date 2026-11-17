using System.Globalization;
using FluentValidation;
using GIRA.Application.DTOs.Reservas;

namespace GIRA.Application.Validations.Reservas;

/// <summary>Valida los filtros del aviso de próxima reserva: mesa y franja obligatorias, fecha en aaaa-mm-dd.</summary>
public class ConsultarAdvertenciaMesaRequestValidator : AbstractValidator<ConsultarAdvertenciaMesaRequest>
{
    /// <summary>Convierte el texto a fecha; solo acepta el formato aaaa-mm-dd.</summary>
    public static bool TryParseFecha(string? texto, out DateOnly fecha) =>
        DateOnly.TryParseExact(texto?.Trim(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out fecha);

    /// <summary>Convierte el texto a un id; no acepta el id vacío.</summary>
    public static bool TryParseId(string? texto, out Guid id) =>
        Guid.TryParse(texto?.Trim(), out id) && id != Guid.Empty;

    /// <summary>Define las reglas de mesa, franja y fecha.</summary>
    public ConsultarAdvertenciaMesaRequestValidator()
    {
        RuleFor(r => r.MesaId)
            .Cascade(CascadeMode.Stop)
            .Must(m => !string.IsNullOrWhiteSpace(m)).WithMessage("La mesa es obligatoria.")
            .Must(m => TryParseId(m, out _)).WithMessage("La mesa indicada no es válida.");

        RuleFor(r => r.FranjaId)
            .Cascade(CascadeMode.Stop)
            .Must(f => !string.IsNullOrWhiteSpace(f)).WithMessage("La franja es obligatoria.")
            .Must(f => TryParseId(f, out _)).WithMessage("La franja indicada no es válida.");

        RuleFor(r => r.Fecha)
            .Must(f => TryParseFecha(f, out _))
            .When(r => !string.IsNullOrWhiteSpace(r.Fecha))
            .WithMessage("La fecha no es válida; usa el formato aaaa-mm-dd.");
    }
}
