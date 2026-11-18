using FluentValidation;
using GIRA.Application.DTOs.Personal;

namespace GIRA.Application.Validations.Personal;

/// <summary>Reglas de formato de una solicitud de ausencia: empleado, tipo, fechas y motivo.</summary>
public class AusenciaRequestValidator : AbstractValidator<AusenciaRequest>
{
    /// <summary>Define las reglas de empleado, tipo, fechas y motivo.</summary>
    public AusenciaRequestValidator()
    {
        RuleFor(x => x.EmpleadoId)
            .NotEqual(Guid.Empty).WithMessage("Debes indicar el empleado.");

        RuleFor(x => x.Tipo)
            .IsInEnum().WithMessage("El tipo de ausencia no es válido.");

        RuleFor(x => x.FechaHasta)
            .GreaterThanOrEqualTo(x => x.FechaDesde)
            .WithMessage("La fecha de inicio no puede ser posterior a la fecha de fin.");

        RuleFor(x => x.Motivo)
            .MaximumLength(250).WithMessage("El motivo no puede superar 250 caracteres.");
    }
}
