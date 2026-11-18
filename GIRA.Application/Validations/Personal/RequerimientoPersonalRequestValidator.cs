using FluentValidation;
using GIRA.Application.DTOs.Personal;

namespace GIRA.Application.Validations.Personal;

/// <summary>Reglas de un requerimiento de personal: el cargo es obligatorio y los valores deben ser mayores que cero (RF-PER-08).</summary>
public class RequerimientoPersonalRequestValidator : AbstractValidator<RequerimientoPersonalRequest>
{
    /// <summary>Define las reglas de cargo, comensales y mínimo por franja.</summary>
    public RequerimientoPersonalRequestValidator()
    {
        RuleFor(x => x.CargoId)
            .NotEqual(Guid.Empty).WithMessage("Debes indicar el cargo.");

        RuleFor(x => x.ComensalesPorEmpleado)
            .GreaterThan(0).WithMessage("Los comensales por empleado deben ser mayores que cero.");

        RuleFor(x => x.MinimoPorFranja)
            .GreaterThan(0).WithMessage("El mínimo por franja debe ser mayor que cero.");
    }
}
