using FluentValidation;
using GIRA.Application.DTOs.Reservas;
using System;
using System.Collections.Generic;
using System.Text;

namespace GIRA.Application.Validations.Reservas;

/// <summary>Número, capacidad (RN-RES-02) y zona de una mesa.</summary>
public class GuardarMesaRequestValidator : AbstractValidator<GuardarMesaRequest>
{
    public const int CapacidadMaxima = 50;

    public GuardarMesaRequestValidator()
    {
        RuleFor(r => r.Numero).GreaterThanOrEqualTo(1).WithMessage("El número de la mesa debe ser 1 o mayor.");
        RuleFor(r => r.Capacidad).InclusiveBetween(1, CapacidadMaxima)
            .WithMessage($"La capacidad debe estar entre 1 y {CapacidadMaxima} personas.");
        RuleFor(r => r.ZonaId).NotEmpty().WithMessage("Selecciona la zona de la mesa.");
    }
}