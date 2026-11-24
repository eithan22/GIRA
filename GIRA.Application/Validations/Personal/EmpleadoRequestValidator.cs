using FluentValidation;
using GIRA.Application.DTOs.Personal;

namespace GIRA.Application.Validations.Personal;

/// <summary>Reglas de formato para registrar o editar un empleado. La unicidad de la cédula y la existencia del cargo las revisa el servicio.</summary>
public class EmpleadoRequestValidator : AbstractValidator<EmpleadoRequest>
{
    /// <summary>Define las reglas de datos personales, cargo y horas.</summary>
    public EmpleadoRequestValidator()
    {
        RuleFor(x => x.Nombre)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre no puede superar 100 caracteres.");

        RuleFor(x => x.Apellido)
            .NotEmpty().WithMessage("El apellido es obligatorio.")
            .MaximumLength(100).WithMessage("El apellido no puede superar 100 caracteres.");

        RuleFor(x => x.Cedula)
            .NotEmpty().WithMessage("La cédula es obligatoria.")
            .MaximumLength(20).WithMessage("La cédula no puede superar 20 caracteres.");

        RuleFor(x => x.Telefono)
            .MaximumLength(20).WithMessage("El teléfono no puede superar 20 caracteres.");

        RuleFor(x => x.Email)
            .EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email))
            .WithMessage("El correo no tiene un formato válido.")
            .MaximumLength(256).WithMessage("El correo no puede superar 256 caracteres.");

        RuleFor(x => x.CargoId)
            .NotEqual(Guid.Empty).WithMessage("Debes indicar el cargo del empleado.");

        RuleFor(x => x.HorasMaximasSemana)
            .InclusiveBetween(1, 168).WithMessage("Las horas máximas por semana deben estar entre 1 y 168.");
    }
}
