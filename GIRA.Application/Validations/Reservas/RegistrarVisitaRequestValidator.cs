using FluentValidation;
using GIRA.Application.DTOs.Reservas;

namespace GIRA.Application.Validations.Reservas;

/// <summary>
/// Valida el registro de una visita sin reserva (GI-166). El máximo con mesa depende de la
/// capacidad de cada mesa, así que lo comprueba <c>VisitaService</c>; sin mesa el tope es el
/// mismo que el de capacidad de una mesa.
/// </summary>
public class RegistrarVisitaRequestValidator : AbstractValidator<RegistrarVisitaRequest>
{
    /// <summary>Define las reglas de franja y personas.</summary>
    public RegistrarVisitaRequestValidator()
    {
        RuleFor(r => r.FranjaHorariaId).NotEmpty().WithMessage("La franja es obligatoria.");

        RuleFor(r => r.Personas)
            .GreaterThanOrEqualTo(1).WithMessage("Debe haber al menos 1 persona.");

        RuleFor(r => r.Personas)
            .LessThanOrEqualTo(GuardarMesaRequestValidator.CapacidadMaxima)
            .When(r => r.MesaId is null)
            .WithMessage($"Sin mesa, el máximo es {GuardarMesaRequestValidator.CapacidadMaxima} personas.");
    }
}
