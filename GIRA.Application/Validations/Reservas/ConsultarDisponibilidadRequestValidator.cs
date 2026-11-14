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
    /// <summary>Nombre del conjunto de reglas de fecha (pasada y fuera de la ventana de reserva).</summary>
    public const string ReglasDeFecha = "Fecha";

    /// <summary>Crea el validador con los límites de tiempo del restaurante.</summary>
    public ConsultarDisponibilidadRequestValidator(IParametrosReservaService parametros, TimeProvider timeProvider)
    {
        // RuleSet "Fecha": solo lo ejecuta el personal (IncludeAllRuleSets) y conserva su 400. Para el cliente una
        // fecha pasada o fuera de la ventana no es un error: son franjas deshabilitadas con su motivo (GI-226).
        RuleSet(ReglasDeFecha, () =>
        {
            RuleFor(r => r.Fecha)
                .GreaterThanOrEqualTo(_ => timeProvider.HoyLocal())
                .WithMessage("La fecha no puede ser en el pasado.")
                // Regla asíncrona: la antelación máxima se lee de la configuración (los servicios validan con ValidateAsync).
                .MustAsync(async (fecha, cancellationToken) =>
                    fecha <= timeProvider.HoyLocal().AddDays((await parametros.ObtenerAsync(cancellationToken)).AntelacionMaxima.Days))
                .WithMessage("La fecha supera la ventana máxima de anticipación.");
        });

        RuleFor(r => r.Personas)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Debe haber al menos 1 persona.");
    }
}
