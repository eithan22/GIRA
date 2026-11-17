using FluentValidation;
using GIRA.Application.DTOs.Reservas;
using System;
using System.Collections.Generic;
using System.Text;

namespace GIRA.Application.Validations.Reservas;

public class GuardarZonaRequestValidator : AbstractValidator<GuardarZonaRequest>
{
    public GuardarZonaRequestValidator()
    {
        RuleFor(r => r.Nombre)
            .NotEmpty().WithMessage("El nombre de la zona es obligatorio.")
            .MaximumLength(100).WithMessage("El nombre de la zona no puede superar los 100 caracteres.");
    }
}