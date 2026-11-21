using FluentValidation;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Services.Reservas;

namespace GIRA.Application.Validations.Reservas;

/// <summary>
/// Valida el registro de una visita sin reserva (GI-166). La mesa es obligatoria (decisión del líder: una
/// mesa admite un solo grupo por franja); su capacidad la comprueba <c>VisitaService</c>, que es quien
/// conoce la mesa real.
/// </summary>
public class RegistrarVisitaRequestValidator : AbstractValidator<RegistrarVisitaRequest>
{
    /// <summary>Define las reglas de franja, mesa y personas.</summary>
    public RegistrarVisitaRequestValidator()
    {
        RuleFor(r => r.FranjaHorariaId).NotEmpty().WithMessage("La franja es obligatoria.");

        // NotEmpty() no sirve aquí: en un Guid? nullable, default(Guid?) es null, así que NotEmpty solo
        // atraparía el null y dejaría pasar Guid.Empty. Must cubre los dos casos con el mismo mensaje.
        RuleFor(r => r.MesaId)
            .Must(id => id is { } valor && valor != Guid.Empty)
            .WithMessage(MensajesReservas.VisitaMesaObligatoria);

        RuleFor(r => r.Personas)
            .GreaterThanOrEqualTo(1).WithMessage("Debe haber al menos 1 persona.");
    }
}
