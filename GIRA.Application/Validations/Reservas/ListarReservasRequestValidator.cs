using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Domain.Enums.Reservas;

namespace GIRA.Application.Validations.Reservas;

/// <summary>Valida los filtros y la paginación de la agenda del personal (GI-164).</summary>
public class ListarReservasRequestValidator : AbstractValidator<ListarReservasRequest>
{
    /// <summary>Define las reglas de paginación, estado y teléfono.</summary>
    public ListarReservasRequestValidator()
    {
        RuleFor(r => r.Page).GreaterThanOrEqualTo(1).WithMessage("La página debe ser 1 o mayor.");
        RuleFor(r => r.PageSize).InclusiveBetween(1, 100).WithMessage("El tamaño de página debe estar entre 1 y 100.");

        RuleFor(r => r.Estado)
            .Must(EsEstadoValido).When(r => !string.IsNullOrWhiteSpace(r.Estado))
            .WithMessage("El estado indicado no es válido.");

        // Mismos textos que CrearReservaRequestValidator, para no decir dos cosas distintas del mismo dato.
        RuleFor(r => r.Telefono)
            .Must(t => !TelefonoRd.EmpiezaConCodigoDePais(TelefonoRd.Normalizar(t!)))
            .When(r => !string.IsNullOrWhiteSpace(r.Telefono))
            .WithMessage("Escribe solo los 10 dígitos del teléfono (809, 829 o 849), sin código de país.")
            .DependentRules(() =>
            {
                RuleFor(r => r.Telefono)
                    .Must(t => TelefonoRd.EsValido(TelefonoRd.Normalizar(t!)))
                    .When(r => !string.IsNullOrWhiteSpace(r.Telefono))
                    .WithMessage("El teléfono debe tener 10 dígitos y empezar con 809, 829 o 849.");
            });
    }

    /// <summary>Convierte el texto al enum; solo acepta nombres, nunca números sueltos.</summary>
    public static bool TryParseEstado(string? texto, out EstadoReserva estado)
    {
        estado = default;
        return !string.IsNullOrWhiteSpace(texto)
            && !int.TryParse(texto, out _)
            && Enum.TryParse(texto.Trim(), ignoreCase: true, out estado)
            && Enum.IsDefined(estado);
    }

    private static bool EsEstadoValido(string? texto) => TryParseEstado(texto, out _);
}
