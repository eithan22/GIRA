using GIRA.Application.DTOs.Reservas;

namespace GIRA.Application.Services.Reservas;

/// <summary>
/// Mensajes de error de Reservas que cambian según quién los lee (cliente o personal). Un solo lugar
/// para que la creación, la modificación y la disponibilidad no redacten cada uno su versión.
/// </summary>
internal static class MensajesReservas
{
    /// <summary>
    /// Grupo mayor que la mesa más grande. El cliente recibe la invitación a llamar; el personal, la
    /// explicación de que esos grupos se atienden por teléfono, fuera del sistema.
    /// </summary>
    public static string GrupoGrande(AudienciaReserva audiencia, int capacidadMaxima) =>
        audiencia == AudienciaReserva.Personal
            ? $"Las mesas admiten como máximo {capacidadMaxima} personas. Los grupos más grandes se atienden por teléfono, fuera del sistema."
            : $"Para grupos de más de {capacidadMaxima} personas, llama al restaurante.";

    /// <summary>El teléfono ya alcanzó el máximo de reservas vigentes (parámetro configurable, 2 por defecto).</summary>
    public static string LimiteVigentesPorTelefono(AudienciaReserva audiencia, int maximo)
    {
        var vigentes = maximo == 1 ? "1 reserva vigente" : $"{maximo} reservas vigentes";
        return audiencia == AudienciaReserva.Personal
            ? $"Este teléfono ya tiene {vigentes}; cancela una para registrar otra."
            : $"Ya tienes {vigentes} con este teléfono; cancela una para reservar otra.";
    }

    /// <summary>El restaurante no abre ese día (día de la semana cerrado o fecha cerrada). Aplica al cliente y al personal.</summary>
    public const string RestauranteCerrado = "El restaurante está cerrado ese día.";

    /// <summary>La franja ya terminó (solo aplica al personal, que no tiene antelación mínima).</summary>
    public const string FranjaTerminadaPersonal = "Esa franja ya terminó; elige una que aún no haya terminado.";

    /// <summary>El cliente no cumple la antelación mínima.</summary>
    public const string SinAntelacionMinima = "Esa franja ya no tiene la antelación mínima requerida.";
}
