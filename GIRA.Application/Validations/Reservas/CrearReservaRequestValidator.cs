using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Services.Reservas;

namespace GIRA.Application.Validations.Reservas;

/// <summary>
/// Valida los datos sueltos de <see cref="CrearReservaRequest"/>: nombre, formato del
/// teléfono, fecha dentro de la ventana y una cantidad de personas válida. No consulta la
/// base de datos (capacidad, franja activa, día cerrado y antelación mínima los revisa el
/// servicio, igual que en GI-157).
/// </summary>
public class CrearReservaRequestValidator : AbstractValidator<CrearReservaRequest>
{
    /// <summary>Crea el validador con los límites de tiempo del restaurante.</summary>
    public CrearReservaRequestValidator(IParametrosReservaService parametros, TimeProvider timeProvider)
    {
        RuleFor(r => r.Nombre)
            .NotEmpty().WithMessage("El nombre es obligatorio.")
            .MaximumLength(150);

        RuleFor(r => r.Telefono)
            .Cascade(CascadeMode.Stop)
            .Must(t => !TelefonoRd.EmpiezaConCodigoDePais(TelefonoRd.Normalizar(t)))
            .WithMessage("Escribe solo los 10 dígitos de tu teléfono (809, 829 o 849), sin código de país.")
            .Must(t => TelefonoRd.EsValido(TelefonoRd.Normalizar(t)))
            .WithMessage("El teléfono debe tener 10 dígitos y empezar con 809, 829 o 849.");

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
