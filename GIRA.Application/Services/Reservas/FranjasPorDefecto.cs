namespace GIRA.Application.Services.Reservas;

/// <summary>
/// Franjas de 2 horas con que se entrega el sistema (GI-226): fuente única para el seeder y las pruebas. Cada
/// franja dura 1 h 45 min y hay 15 min de hueco entre las contiguas; todas caben en el horario por defecto
/// (12:00–22:30). La migración <c>Reservas_FranjasDeDosHoras</c> repite estos valores en SQL porque una migración
/// no puede depender del código que cambia después.
/// </summary>
public static class FranjasPorDefecto
{
    /// <summary>Definición de una franja por defecto.</summary>
    public sealed record Definicion(Guid Id, string Nombre, TimeOnly HoraInicio, TimeOnly HoraFin);

    /// <summary>Almuerzo 12:00–13:45. Conserva el Id de la franja original.</summary>
    public static readonly Definicion Almuerzo = new(
        Guid.Parse("00000000-0000-0000-0000-000000000001"), "Almuerzo", new TimeOnly(12, 0), new TimeOnly(13, 45));

    /// <summary>Tarde 14:00–15:45.</summary>
    public static readonly Definicion Tarde = new(
        Guid.Parse("00000000-0000-0000-0000-000000000003"), "Tarde", new TimeOnly(14, 0), new TimeOnly(15, 45));

    /// <summary>Cena 18:30–20:15. Conserva el Id de la franja original.</summary>
    public static readonly Definicion Cena = new(
        Guid.Parse("00000000-0000-0000-0000-000000000002"), "Cena", new TimeOnly(18, 30), new TimeOnly(20, 15));

    /// <summary>Noche 20:30–22:15.</summary>
    public static readonly Definicion Noche = new(
        Guid.Parse("00000000-0000-0000-0000-000000000004"), "Noche", new TimeOnly(20, 30), new TimeOnly(22, 15));

    /// <summary>Las cuatro franjas, por hora de inicio.</summary>
    public static IReadOnlyList<Definicion> Todas { get; } = [Almuerzo, Tarde, Cena, Noche];
}
