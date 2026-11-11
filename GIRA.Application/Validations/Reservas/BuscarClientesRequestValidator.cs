using FluentValidation;
using GIRA.Application.DTOs.Reservas;

namespace GIRA.Application.Validations.Reservas;

/// <summary>Valida la búsqueda de clientes del personal (GI-164): texto mínimo y paginación.</summary>
public class BuscarClientesRequestValidator : AbstractValidator<BuscarClientesRequest>
{
    /// <summary>Cantidad mínima de caracteres para buscar, para no volcar toda la tabla de clientes.</summary>
    public const int MinimoCaracteres = 3;

    /// <summary>Define las reglas del texto y la paginación.</summary>
    public BuscarClientesRequestValidator()
    {
        RuleFor(r => r.Buscar)
            .Must(b => b is not null && b.Trim().Length >= MinimoCaracteres)
            .WithMessage($"Escribe al menos {MinimoCaracteres} caracteres para buscar.");
        RuleFor(r => r.Page).GreaterThanOrEqualTo(1).WithMessage("La página debe ser 1 o mayor.");
        RuleFor(r => r.PageSize).InclusiveBetween(1, 100).WithMessage("El tamaño de página debe estar entre 1 y 100.");
    }
}
