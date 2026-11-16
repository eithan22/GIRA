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

    /// <summary>La franja de hoy ya terminó o la fecha ya pasó (motivo de disponibilidad para el cliente).</summary>
    public const string FranjaYaPaso = "Esta franja ya pasó.";

    /// <summary>La franja empieza antes de la antelación mínima configurada (motivo de disponibilidad para el cliente).</summary>
    public static string RequiereAntelacion(TimeSpan antelacionMinima) =>
        $"Requiere reservar con al menos {Duracion(antelacionMinima)} de antelación.";

    /// <summary>La fecha supera la antelación máxima configurada (motivo de disponibilidad para el cliente).</summary>
    public static string FueraDelRango(int maximoDias) =>
        $"Fuera del rango de reserva (máximo {maximoDias} {(maximoDias == 1 ? "día" : "días")}).";

    /// <summary>No queda una mesa libre con capacidad para el grupo (motivo de disponibilidad para el cliente).</summary>
    public static string SinMesas(int personas) =>
        $"Sin mesas disponibles para {personas} {(personas == 1 ? "persona" : "personas")}.";

    /// <summary>Duración en texto: "1 hora", "2 horas", "1 minuto" o "90 minutos".</summary>
    public static string Duracion(TimeSpan duracion)
    {
        if (duracion.Days == 0 && duracion.Minutes == 0 && duracion.Hours > 0)
        {
            return duracion.Hours == 1 ? "1 hora" : $"{duracion.Hours} horas";
        }

        var minutosTotales = (int)duracion.TotalMinutes;
        return minutosTotales == 1 ? "1 minuto" : $"{minutosTotales} minutos";
    }

    /// <summary>El restaurante no abre ese día (día de la semana cerrado o fecha cerrada). Aplica al cliente y al personal.</summary>
    public const string RestauranteCerrado = "El restaurante está cerrado ese día.";

    /// <summary>La franja ya terminó (solo aplica al personal, que no tiene antelación mínima).</summary>
    public const string FranjaTerminadaPersonal = "Esa franja ya terminó; elige una que aún no haya terminado.";

    /// <summary>El cliente no cumple la antelación mínima.</summary>
    public const string SinAntelacionMinima = "Esa franja ya no tiene la antelación mínima requerida.";

    /// <summary>
    /// La mesa elegida a mano tiene una visita sin reserva en esa fecha y franja: cuenta como ocupante
    /// aunque no tenga hora de salida. Solo lo ve el personal, que es quien elige la mesa.
    /// </summary>
    public static string MesaConVisita(int numeroMesa) =>
        $"La mesa {numeroMesa} tiene una visita sin reserva en esta franja. Elige otra mesa o franja.";

    /// <summary>
    /// Aviso (no bloqueante) al registrar una visita: la mesa tiene una reserva vigente en una franja
    /// posterior del mismo día. <paramref name="minutosFaltantes"/> es null si la fecha consultada no es
    /// hoy (ver <see cref="GIRA.Application.DTOs.Reservas.AdvertenciaMesaDto"/>).
    /// </summary>
    public static string AvisoProximaReserva(
        int numeroMesa, TimeOnly horaInicio, string franjaNombre, int personas, int? minutosFaltantes)
    {
        var hora = horaInicio.ToString(@"HH\:mm");
        var personasTexto = personas == 1 ? "1 persona" : $"{personas} personas";
        var minutosTexto = minutosFaltantes is int m
            ? $", en {FormatearTiempoFaltante(m)}"
            : "";
        return $"La mesa {numeroMesa} tiene una reserva a las {hora} (franja {franjaNombre}, {personasTexto})" +
            $"{minutosTexto}. Si registras la visita, la mesa podría no estar libre a tiempo.";
    }

    // Menos de 60: "M min"; hora exacta: "H h"; el resto: "H h M min" (SRS v1.4 §5.2).
    private static string FormatearTiempoFaltante(int minutos)
    {
        if (minutos < 60)
        {
            return $"{minutos} min";
        }

        var horas = minutos / 60;
        var restoMinutos = minutos % 60;
        return restoMinutos == 0 ? $"{horas} h" : $"{horas} h {restoMinutos} min";
    }

    /// <summary>Falta la fecha al consultar la disponibilidad (la consulta sin <c>fecha</c> llega como 0001-01-01).</summary>
    public const string FechaObligatoria = "La fecha es obligatoria.";

    /// <summary>Visita sin reserva con el día o la fecha cerrados (no hay horario de operación hoy).</summary>
    public const string VisitaRestauranteCerradoHoy =
        "El restaurante está cerrado hoy; no se pueden registrar visitas sin reserva.";

    /// <summary>Visita sin reserva fuera del horario de operación de hoy (antes de abrir o después de cerrar).</summary>
    public static string VisitaRestauranteCerradoAhora(TimeOnly apertura, TimeOnly cierre) =>
        $"El restaurante está cerrado ahora; el horario de hoy es de {apertura:HH\\:mm} a {cierre:HH\\:mm}. " +
        "No se pueden registrar visitas sin reserva fuera de horario.";

    /// <summary>
    /// La franja pedida para la visita no es la más reciente de las que ya empezaron: la visita se registra en la
    /// franja actual o, entre franjas, en la última que empezó.
    /// </summary>
    public static string VisitaFranjaNoVigente(string franjaNombre, TimeOnly inicio, TimeOnly fin) =>
        $"Esa franja ya no es la actual: la visita se registra en la franja {franjaNombre} ({inicio:HH\\:mm}–{fin:HH\\:mm}).";

    /// <summary>La zona elegida (GI-4xx) ya no tiene mesas disponibles en esa franja.</summary>
    public static string ZonaSinMesas(string nombreZona) =>
        $"La zona {nombreZona} ya no tiene mesas disponibles en esa franja. Elige otra zona o \"sin preferencia\".";

    /// <summary>El canal del personal envió ZonaId (C3): ese canal no usa zona, la mesa la asigna el sistema.</summary>
    public const string ZonaNoAplicaPersonal = "El canal del personal no usa zona; la mesa la asigna el sistema.";
}
