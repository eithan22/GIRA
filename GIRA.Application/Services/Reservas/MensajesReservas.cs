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

    /// <summary>Ya hay 2 reservas vigentes con ese teléfono.</summary>
    public static string LimiteVigentesPorTelefono(AudienciaReserva audiencia) =>
        audiencia == AudienciaReserva.Personal
            ? "Este teléfono ya tiene 2 reservas vigentes; cancela una para registrar otra."
            : "Ya tienes 2 reservas vigentes con este teléfono; cancela una para reservar otra.";

    /// <summary>La franja ya terminó (solo aplica al personal, que no tiene antelación mínima).</summary>
    public const string FranjaTerminadaPersonal = "Esa franja ya terminó; elige una que aún no haya terminado.";

    /// <summary>El cliente no cumple la antelación mínima.</summary>
    public const string SinAntelacionMinima = "Esa franja ya no tiene la antelación mínima requerida.";
}
