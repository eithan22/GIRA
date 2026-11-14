using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Configuracion;

namespace GIRA.Application.Validations.Configuracion;

/// <summary>
/// Valida las ocho políticas de reserva (GI-226, GI-404): rangos y relaciones entre ellas. El controlador de
/// Configuración ejecuta este validador y devuelve un <c>ValidationProblem</c> con el error junto a cada campo.
/// </summary>
public class ActualizarParametrosRequestValidator : AbstractValidator<ActualizarParametrosRequest>
{
    private const int MinutosPorDia = 24 * 60;

    /// <summary>Define las reglas de obligatoriedad, rango y relación entre antelaciones y límite de cancelación.</summary>
    public ActualizarParametrosRequestValidator()
    {
        // Los siete campos numéricos son obligatorios: omitirlos en el JSON no debe guardarse como 0.
        RuleFor(r => r.AntelacionMinimaMinutos).NotNull().WithMessage("La antelación mínima es obligatoria.");
        RuleFor(r => r.AntelacionMaximaDias).NotNull().WithMessage("La antelación máxima es obligatoria.");
        RuleFor(r => r.LimiteCancelacionMinutos).NotNull().WithMessage("El límite de cancelación es obligatorio.");
        RuleFor(r => r.VentanaCheckInMinutos).NotNull().WithMessage("La ventana de check-in es obligatoria.");
        RuleFor(r => r.ToleranciaNoShowMinutos).NotNull().WithMessage("La tolerancia de no-show es obligatoria.");
        RuleFor(r => r.MaxReservasVigentesPorTelefono).NotNull().WithMessage("El máximo de reservas vigentes por teléfono es obligatorio.");
        RuleFor(r => r.MaxPersonasPorReserva).NotNull().WithMessage("El tamaño máximo de grupo es obligatorio.");

        RuleFor(r => r.AntelacionMaximaDias)
            .InclusiveBetween(1, 365)
            .When(r => r.AntelacionMaximaDias is not null)
            .WithMessage("La antelación máxima debe estar entre 1 y 365 días.");

        RuleFor(r => r.AntelacionMinimaMinutos)
            .GreaterThanOrEqualTo(0)
            .When(r => r.AntelacionMinimaMinutos is not null)
            .WithMessage("La antelación mínima no puede ser negativa.")
            .Must((r, minimaMinutos) => minimaMinutos < (long)r.AntelacionMaximaDias!.Value * MinutosPorDia)
            .When(r => r.AntelacionMinimaMinutos is not null && r.AntelacionMaximaDias is >= 1 and <= 365, ApplyConditionTo.CurrentValidator)
            .WithMessage("La antelación mínima debe ser menor que la antelación máxima.");

        RuleFor(r => r.LimiteCancelacionMinutos)
            .GreaterThanOrEqualTo(0)
            .When(r => r.LimiteCancelacionMinutos is not null)
            .WithMessage("El límite de cancelación no puede ser negativo.")
            .Must((r, limiteMinutos) => limiteMinutos <= (long)r.AntelacionMaximaDias!.Value * MinutosPorDia)
            .When(r => r.LimiteCancelacionMinutos is not null && r.AntelacionMaximaDias is >= 1 and <= 365, ApplyConditionTo.CurrentValidator)
            .WithMessage("El límite de cancelación no puede superar la antelación máxima.");

        RuleFor(r => r.VentanaCheckInMinutos)
            .InclusiveBetween(0, 120)
            .When(r => r.VentanaCheckInMinutos is not null)
            .WithMessage("La ventana de check-in debe estar entre 0 y 120 minutos.");

        RuleFor(r => r.ToleranciaNoShowMinutos)
            .InclusiveBetween(0, 120)
            .When(r => r.ToleranciaNoShowMinutos is not null)
            .WithMessage("La tolerancia de no-show debe estar entre 0 y 120 minutos.");

        RuleFor(r => r.MaxReservasVigentesPorTelefono)
            .InclusiveBetween(1, 10)
            .When(r => r.MaxReservasVigentesPorTelefono is not null)
            .WithMessage("El máximo de reservas vigentes por teléfono debe estar entre 1 y 10.");

        RuleFor(r => r.MaxPersonasPorReserva)
            .InclusiveBetween(1, 50)
            .When(r => r.MaxPersonasPorReserva is not null)
            .WithMessage("El tamaño máximo de grupo debe estar entre 1 y 50 personas.");

        RuleFor(r => r.TelefonoContacto)
            .MaximumLength(30).WithMessage("El teléfono de contacto no puede superar los 30 caracteres.")
            .Matches(@"^[0-9+\-().\s]*$").WithMessage("El teléfono de contacto solo puede tener dígitos, espacios, +, -, ( ) y punto.");
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
