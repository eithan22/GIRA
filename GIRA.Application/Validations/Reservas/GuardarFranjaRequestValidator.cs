using FluentValidation;
using GIRA.Application.DTOs.Reservas;
using System;
using System.Collections.Generic;
using System.Text;

namespace GIRA.Application.Validations.Reservas;


public class GuardarFranjaRequestValidator : AbstractValidator<GuardarFranjaRequest>
{
    public GuardarFranjaRequestValidator()
    {
        RuleFor(r => r.Nombre)
            .NotEmpty().WithMessage("El nombre de la franja es obligatorio.")
            .MaximumLength(50).WithMessage("El nombre de la franja no puede superar los 50 caracteres.");
        RuleFor(r => r.HoraFin).GreaterThan(r => r.HoraInicio)
            .WithMessage("La hora de fin debe ser posterior a la hora de inicio.");
    }
}