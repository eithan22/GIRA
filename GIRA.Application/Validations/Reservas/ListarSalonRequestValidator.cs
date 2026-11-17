using FluentValidation;
using GIRA.Application.DTOs.Reservas;
using System;
using System.Collections.Generic;
using System.Text;

namespace GIRA.Application.Validations.Reservas;

/// <summary>Valida la paginación; también aplica a ListarMesasRequest.</summary>
public class ListarSalonRequestValidator : AbstractValidator<ListarSalonRequest>
{
    public ListarSalonRequestValidator()
    {
        RuleFor(r => r.Page).GreaterThanOrEqualTo(1).WithMessage("La página debe ser 1 o mayor.");
        RuleFor(r => r.PageSize).InclusiveBetween(1, 100).WithMessage("El tamaño de página debe estar entre 1 y 100.");
    }
}
