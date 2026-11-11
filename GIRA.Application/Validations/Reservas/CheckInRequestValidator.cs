using FluentValidation;
using GIRA.Application.DTOs.Reservas;

namespace GIRA.Application.Validations.Reservas;

/// <summary>
/// Valida el check-in (GI-165). El máximo depende de la mesa de cada reserva, así que ese
/// límite (RN-RES-15) lo comprueba <c>ReservaService.CheckInAsync</c>, no este validador.
/// </summary>
public class CheckInRequestValidator : AbstractValidator<CheckInRequest>
{
    /// <summary>Define la regla del mínimo de personas.</summary>
    public CheckInRequestValidator()
    {
        RuleFor(r => r.PersonasReales)
            .GreaterThanOrEqualTo(1).WithMessage("Debe haber al menos 1 persona.");
    }
}
