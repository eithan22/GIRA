using FluentValidation;
using GIRA.Application.DTOs.Seguridad;

namespace GIRA.Application.Validations.Seguridad;

public class CrearUsuarioRequestValidator : AbstractValidator<CrearUsuarioRequest>
{
    public CrearUsuarioRequestValidator()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo es obligatorio.")
            .EmailAddress().WithMessage("El correo no tiene un formato válido.")
            .MaximumLength(256).WithMessage("El correo no puede superar 256 caracteres.");

        RuleFor(x => x.NombreCompleto)
            .NotEmpty().WithMessage("El nombre completo es obligatorio.")
            .MaximumLength(150).WithMessage("El nombre no puede superar 150 caracteres.");

        RuleFor(x => x.EmpleadoId)
            .NotEqual(Guid.Empty).When(x => x.EmpleadoId.HasValue)
            .WithMessage("El empleado vinculado no es válido.");
    }
}