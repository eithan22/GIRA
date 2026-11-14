using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Configuracion;

namespace GIRA.Application.Validations.Configuracion;

/// <summary>
/// Valida las seis políticas de reserva (GI-226): rangos y relaciones entre ellas. El controlador de
/// Configuración ejecuta este validador y devuelve un <c>ValidationProblem</c> con el error junto a cada campo.
/// </summary>
public class ActualizarParametrosRequestValidator : AbstractValidator<ActualizarParametrosRequest>
{
    private const int MinutosPorDia = 24 * 60;

    /// <summary>Define las reglas de rango y de relación entre antelaciones y límite de cancelación.</summary>
    public ActualizarParametrosRequestValidator()
    {
        RuleFor(r => r.AntelacionMaximaDias)
            .InclusiveBetween(1, 365)
            .WithMessage("La antelación máxima debe estar entre 1 y 365 días.");

        RuleFor(r => r.AntelacionMinimaMinutos)
            .GreaterThanOrEqualTo(0)
            .WithMessage("La antelación mínima no puede ser negativa.")
            .Must((r, minimaMinutos) => minimaMinutos < (long)r.AntelacionMaximaDias * MinutosPorDia)
            .When(r => r.AntelacionMaximaDias is >= 1 and <= 365, ApplyConditionTo.CurrentValidator)
            .WithMessage("La antelación mínima debe ser menor que la antelación máxima.");

        RuleFor(r => r.LimiteCancelacionMinutos)
            .GreaterThanOrEqualTo(0)
            .WithMessage("El límite de cancelación no puede ser negativo.")
            .Must((r, limiteMinutos) => limiteMinutos <= (long)r.AntelacionMaximaDias * MinutosPorDia)
            .When(r => r.AntelacionMaximaDias is >= 1 and <= 365, ApplyConditionTo.CurrentValidator)
            .WithMessage("El límite de cancelación no puede superar la antelación máxima.");

        RuleFor(r => r.VentanaCheckInMinutos)
            .InclusiveBetween(0, 120)
            .WithMessage("La ventana de check-in debe estar entre 0 y 120 minutos.");

        RuleFor(r => r.MaxReservasVigentesPorTelefono)
            .InclusiveBetween(1, 10)
            .WithMessage("El máximo de reservas vigentes por teléfono debe estar entre 1 y 10.");

        RuleFor(r => r.MaxPersonasPorReserva)
            .InclusiveBetween(1, 50)
            .WithMessage("El tamaño máximo de grupo debe estar entre 1 y 50 personas.");
    }
}

/// <summary>Valida el horario de un día: si está abierto, la apertura debe ser anterior al cierre.</summary>
public class ActualizarHorarioRequestValidator : AbstractValidator<ActualizarHorarioRequest>
{
    /// <summary>Define la regla de apertura anterior al cierre (solo si el día está abierto).</summary>
    public ActualizarHorarioRequestValidator()
    {
        RuleFor(r => r.HoraApertura)
            .LessThan(r => r.HoraCierre)
            .When(r => r.Abierto)
            .WithMessage("La hora de apertura debe ser anterior a la hora de cierre.");
    }
}

/// <summary>Valida un día cerrado: fecha de hoy en adelante y motivo de hasta 200 caracteres.</summary>
public class CrearDiaCerradoRequestValidator : AbstractValidator<CrearDiaCerradoRequest>
{
    /// <summary>Máximo de caracteres del motivo.</summary>
    public const int MaximoMotivo = 200;

    /// <summary>Define las reglas de fecha y motivo.</summary>
    public CrearDiaCerradoRequestValidator(TimeProvider timeProvider)
    {
        RuleFor(r => r.Fecha)
            .Must(fecha => fecha != default)
            .WithMessage("La fecha es obligatoria.")
            .GreaterThanOrEqualTo(_ => timeProvider.HoyLocal())
            .WithMessage("La fecha no puede ser en el pasado.");

        RuleFor(r => r.Motivo)
            .MaximumLength(MaximoMotivo)
            .WithMessage($"El motivo no puede superar los {MaximoMotivo} caracteres.");
    }
}
