using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Services.Reservas;

namespace GIRA.Application.Validations.Reservas;

/// <summary>
/// Valida los datos sueltos de <see cref="ConsultarDisponibilidadRequest"/>: fecha no pasada,
/// dentro de la ventana de antelación máxima y una cantidad de personas válida. No consulta
/// la base de datos (la capacidad real de las mesas la revisa el servicio).
/// </summary>
public class ConsultarDisponibilidadRequestValidator : AbstractValidator<ConsultarDisponibilidadRequest>
{
    /// <summary>Crea el validador con los límites de tiempo del restaurante.</summary>
    public ConsultarDisponibilidadRequestValidator(IParametrosReservaService parametros, TimeProvider timeProvider)
    {
        RuleFor(r => r.Fecha)
            .GreaterThanOrEqualTo(_ => timeProvider.HoyLocal())
            .WithMessage("La fecha no puede ser en el pasado.")
            // Regla asíncrona: la antelación máxima se lee de la configuración (los servicios validan con ValidateAsync).
            .MustAsync(async (fecha, cancellationToken) =>
                fecha <= timeProvider.HoyLocal().AddDays((await parametros.ObtenerAsync(cancellationToken)).AntelacionMaxima.Days))
            .WithMessage("La fecha supera la ventana máxima de anticipación.");

        RuleFor(r => r.Personas)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Debe haber al menos 1 persona.");
    }
}
