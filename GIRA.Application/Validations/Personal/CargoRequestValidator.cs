using FluentValidation;
using GIRA.Application.DTOs.Personal;

namespace GIRA.Application.Validations.Personal;

/// <summary>Reglas de formato para crear o editar un cargo. La unicidad del nombre la revisa el servicio.</summary>
public class CargoRequestValidator : AbstractValidator<CargoRequest>
{
    /// <summary>Define las reglas de nombre y descripción.</summary>
    public CargoRequestValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre del cargo es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre del cargo no puede superar 100 caracteres.");

        RuleFor(x => x.Descripcion)
            .MaximumLength(250).WithMessage("La descripción no puede superar 250 caracteres.");
    }
}
