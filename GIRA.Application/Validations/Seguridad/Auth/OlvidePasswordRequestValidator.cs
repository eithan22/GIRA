using FluentValidation;
using GIRA.Application.DTOs.Seguridad.Auth;

namespace GIRA.Application.Validations.Seguridad.Auth;

/// <summary>Valida el formato del correo antes de consultar la base.</summary>
public class OlvidePasswordRequestValidator : AbstractValidator<OlvidePasswordRequest>
{
    /// <summary>Define las reglas del correo.</summary>
    public OlvidePasswordRequestValidator()
    {
        // 256 es el mismo largo que acepta el login y que guarda RegistroAcceso.CorreoIntentado.
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("El correo es obligatorio.")
            .MaximumLength(256).WithMessage("El correo no puede superar 256 caracteres.")
            .EmailAddress().WithMessage("El correo no tiene un formato válido.");
    }
}
