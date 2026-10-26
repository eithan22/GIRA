using FluentValidation;
using GIRA.Application.DTOs.Seguridad;

namespace GIRA.Application.Validations.Seguridad;

/// <summary>Valida la paginación y el rango de fechas del historial de accesos.</summary>
public class HistorialAccesosRequestValidator : AbstractValidator<HistorialAccesosRequest>
{
    /// <summary>Define las reglas de página, tamaño de página y fechas.</summary>
    public HistorialAccesosRequestValidator()
    {
        RuleFor(x => x.Page)
            .InclusiveBetween(1, 1_000_000).WithMessage("La página debe estar entre 1 y 1.000.000.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100).WithMessage("El tamaño de página debe estar entre 1 y 100.");

        RuleFor(x => x)
            .Must(x => x.Hasta!.Value >= x.Desde!.Value)
            .When(x => x.Desde.HasValue && x.Hasta.HasValue)
            .WithMessage("La fecha 'hasta' no puede ser anterior a la fecha 'desde'.");
    }
}
