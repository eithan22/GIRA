using FluentValidation;
using GIRA.Application.DTOs.Reservas;

namespace GIRA.Application.Validations.Reservas;

/// <summary>
/// Valida los datos sueltos de la modificación (GI-160). La ventana de fechas y la antelación solo
/// aplican si cambia el horario, y la capacidad depende de la mesa, así que las revisa el servicio.
/// </summary>
public class ModificarReservaRequestValidator : AbstractValidator<ModificarReservaRequest>
{
    /// <summary>Define las reglas de fecha, franja y personas.</summary>
    public ModificarReservaRequestValidator()
    {
        RuleFor(r => r.Fecha).NotEqual(default(DateOnly)).WithMessage("La fecha es obligatoria.");
        RuleFor(r => r.FranjaHorariaId).NotEmpty().WithMessage("La franja es obligatoria.");
        RuleFor(r => r.Personas).GreaterThanOrEqualTo(1).WithMessage("Debe haber al menos 1 persona.");
    }
}
