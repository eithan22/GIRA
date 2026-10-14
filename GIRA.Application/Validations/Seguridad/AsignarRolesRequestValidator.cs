using FluentValidation;
using GIRA.Application.DTOs.Seguridad;

namespace GIRA.Application.Validations.Seguridad;

/// <summary>Valida la lista de roles antes de consultar la base; la existencia de cada rol la comprueba el servicio.</summary>
public class AsignarRolesRequestValidator : AbstractValidator<AsignarRolesRequest>
{
    /// <summary>Define las reglas de la lista de roles.</summary>
    public AsignarRolesRequestValidator()
    {
        RuleFor(x => x.Roles)
            .NotEmpty().WithMessage("El usuario debe quedar con al menos un rol.");

        RuleForEach(x => x.Roles)
            .NotEmpty().WithMessage("El nombre del rol no puede estar vacío.");
    }
}
