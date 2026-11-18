using FluentValidation;
using GIRA.Application.DTOs.Personal;

namespace GIRA.Application.Validations.Personal;

/// <summary>Reglas de formato de un bloque de disponibilidad. El solapamiento lo revisa el servicio.</summary>
public class DisponibilidadRequestValidator : AbstractValidator<DisponibilidadRequest>
{
    /// <summary>Define las reglas de día y de horas.</summary>
    public DisponibilidadRequestValidator()
    {
        RuleFor(x => x.DiaSemana)
            .IsInEnum().WithMessage("El día de la semana no es válido.");

        RuleFor(x => x.HoraFin)
            .GreaterThan(x => x.HoraInicio)
            .WithMessage("La hora de inicio debe ser anterior a la hora de fin.");
    }
}
