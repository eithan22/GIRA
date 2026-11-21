using FluentValidation;
using GIRA.Application.DTOs.Reservas;

namespace GIRA.Application.Validations.Reservas;

/// <summary>Valida la cancelación por el personal (GI-223): motivo obligatorio, de 1 a 500 caracteres ya recortado.</summary>
public class CancelarPorPersonalRequestValidator : AbstractValidator<CancelarPorPersonalRequest>
{
    /// <summary>Máximo de caracteres del motivo (coincide con la columna de la base de datos).</summary>
    public const int MaximoMotivo = 500;

    /// <summary>Define las reglas del motivo.</summary>
    public CancelarPorPersonalRequestValidator()
    {
        RuleFor(r => r.Motivo)
            .Must(m => !string.IsNullOrWhiteSpace(m))
            .WithMessage("El motivo de la cancelación es obligatorio.")
            .DependentRules(() =>
            {
                RuleFor(r => r.Motivo)
                    .Must(m => m.Trim().Length <= MaximoMotivo)
                    .WithMessage($"El motivo no puede superar {MaximoMotivo} caracteres.");
            });
    }
}
