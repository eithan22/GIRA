using FluentValidation;
using GIRA.Application.DTOs.Seguridad;

namespace GIRA.Application.Validations.Seguridad;

public class RegistroUsuarioRequestValidator : AbstractValidator<RegistroUsuarioRequest>
{
    public RegistroUsuarioRequestValidator()
    {
        RuleFor(request => request.NombreCompleto)
            .Must(name => !string.IsNullOrWhiteSpace(name)).WithMessage("El nombre es obligatorio.")
            .MaximumLength(120).WithMessage("El nombre no puede superar 120 caracteres.");

        RuleFor(request => request.Email)
            .NotEmpty().WithMessage("El correo es obligatorio.")
            .MaximumLength(256).WithMessage("El correo no puede superar 256 caracteres.")
            .EmailAddress().WithMessage("El correo no tiene un formato válido.");

        RuleFor(request => request.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MaximumLength(128).WithMessage("La contraseña no puede superar 128 caracteres.");
    }
}
