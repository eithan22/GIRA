namespace GIRA.Application.DTOs.Reservas;

/// <summary>
/// Estados posibles de una mesa en el mapa de ocupación (GI-167). Van como texto, no como número: el proyecto
/// no registra <c>JsonStringEnumConverter</c>, así que se resuelve local a estos DTOs (mismo criterio que el
/// resto de DTOs de reservas).
/// </summary>
public static class EstadoMesaOcupacion
{
    /// <summary>Activa y sin reserva vigente en esa fecha y franja.</summary>
    public const string Libre = "Libre";

    /// <summary>Tiene una reserva Confirmada.</summary>
    public const string Reservada = "Reservada";

    /// <summary>Tiene una reserva EnCurso (ya hizo check-in).</summary>
    public const string Ocupada = "Ocupada";

    /// <summary>La mesa o su zona están inactivas; gana sobre cualquier reserva.</summary>
    public const string Inactiva = "Inactiva";
}

/// <summary>Una mesa en el mapa de ocupación. Sin teléfono: se obtiene con el detalle de la reserva.</summary>
/// <param name="MesaId">Id de la mesa.</param>
/// <param name="Numero">Número de la mesa.</param>
/// <param name="Capacidad">Capacidad de la mesa.</param>
/// <param name="Estado">Una de las constantes de <see cref="EstadoMesaOcupacion"/>.</param>
/// <param name="ReservaId">Reserva vigente de esa mesa, si la hay (también en una mesa inactiva, para ver el conflicto).</param>
/// <param name="ClienteNombre">Nombre del cliente de esa reserva.</param>
/// <param name="Personas">Personas reservadas.</param>
/// <param name="PersonasReales">Personas que llegaron (solo si ya hizo check-in).</param>
public record MesaOcupacionDto(
    Guid MesaId,
    int Numero,
    int Capacidad,
    string Estado,
    Guid? ReservaId,
    string? ClienteNombre,
    int? Personas,
    int? PersonasReales);

/// <summary>Una zona con sus mesas (incluidas las inactivas), ordenadas por número.</summary>
public record ZonaOcupacionDto(Guid ZonaId, string Nombre, bool Activa, IReadOnlyList<MesaOcupacionDto> Mesas);

/// <summary>Cantidad de mesas por estado; la suma es el total de mesas del mapa.</summary>
public record ResumenOcupacionDto(int Libres, int Reservadas, int Ocupadas, int Inactivas);

/// <summary>Mapa de ocupación de mesas para una fecha y una franja (GI-167).</summary>
public record MapaOcupacionDto(
    DateOnly Fecha,
    Guid FranjaId,
    string FranjaNombre,
    TimeOnly HoraInicio,
    TimeOnly HoraFin,
    ResumenOcupacionDto Resumen,
    IReadOnlyList<ZonaOcupacionDto> Zonas);
