using FluentValidation;
using GIRA.Application.DTOs.Configuracion;

namespace GIRA.Application.Validations.Configuracion;

/// <summary>Valida los parámetros de Inventario: obligatorios y dentro de rango.</summary>
public class ActualizarParametrosInventarioRequestValidator : AbstractValidator<ActualizarParametrosInventarioRequest>
{
    /// <summary>Días de proyección entre 1 y 14 (lo que alcanza la proyección); aviso de vencimiento entre 1 y 30.</summary>
    public ActualizarParametrosInventarioRequestValidator()
    {
        RuleFor(r => r.DiasProyeccionInventario)
            .NotNull().WithMessage("Los días de proyección son obligatorios.")
            .InclusiveBetween(1, 14).WithMessage("Los días de proyección deben estar entre 1 y 14.");

        RuleFor(r => r.DiasAvisoVencimientoInventario)
            .NotNull().WithMessage("Los días de aviso de vencimiento son obligatorios.")
            .InclusiveBetween(1, 30).WithMessage("Los días de aviso de vencimiento deben estar entre 1 y 30.");
    }
}
