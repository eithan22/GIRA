using FluentValidation;
using GIRA.Application.DTOs.Seguridad;

namespace GIRA.Application.Validations.Seguridad;

/// <summary>Valida el formato de las credenciales antes de consultar la base.</summary>
public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    /// <summary>Define las reglas del correo y la contraseña.</summary>
    public LoginRequestValidator()
    {
        // 256 es el largo de RegistroAcceso.CorreoIntentado, donde se guarda cada intento.
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo es obligatorio.")
            .MaximumLength(256).WithMessage("El correo no puede superar 256 caracteres.")
            .EmailAddress().WithMessage("El correo no tiene un formato válido.");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("La contraseña es obligatoria.")
            .MaximumLength(128).WithMessage("La contraseña no puede superar 128 caracteres.");
    }
}
