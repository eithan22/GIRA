using FluentValidation;
using GIRA.Application.DTOs.Seguridad.Usuarios;

namespace GIRA.Application.Validations.Seguridad;

public class UsuarioFiltroRequestValidator : AbstractValidator<UsuarioFiltroRequest>
{
    public UsuarioFiltroRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1).WithMessage("La página debe ser 1 o mayor.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("El tamaño de página debe estar entre 1 y 100.");
    }
}