using FluentValidation;
using GIRA.Application.DTOs.Personal;

namespace GIRA.Application.Validations.Personal;

/// <summary>Reglas de formato de un turno. Las reglas que dependen de datos (disponibilidad, solapamiento, ausencias) las revisa el servicio.</summary>
public class TurnoRequestValidator : AbstractValidator<TurnoRequest>
{
    /// <summary>Define las reglas de empleado, cargo, horario y notas.</summary>
    public TurnoRequestValidator()
    {
        RuleFor(x => x.EmpleadoId)
            .NotEqual(Guid.Empty).WithMessage("Debes indicar el empleado del turno.");

        RuleFor(x => x.CargoId)
            .NotEqual(Guid.Empty).WithMessage("Debes indicar el cargo del turno.");

        RuleFor(x => x.HoraFin)
            .GreaterThan(x => x.HoraInicio)
            .WithMessage("La hora de inicio debe ser anterior a la hora de fin. Un turno no puede cruzar la medianoche.");

        RuleFor(x => x.Notas)
            .MaximumLength(250).WithMessage("Las notas no pueden superar 250 caracteres.");
    }
}
