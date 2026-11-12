namespace GIRA.Application.DTOs.Reservas;

/// <summary>
/// Visitas sin reserva de un día (GI-402): totales de todo el día y la lista de las más recientes. Los totales
/// cuentan TODAS las visitas del día aunque la lista se recorte a <see cref="Visitas"/> (tope de 200).
/// </summary>
/// <param name="Fecha">Día listado.</param>
/// <param name="TotalVisitas">Cantidad de visitas del día.</param>
/// <param name="TotalPersonas">Suma de las personas atendidas en el día.</param>
/// <param name="Truncada">True si hay más visitas que las que trae <paramref name="Visitas"/>.</param>
/// <param name="Visitas">Las más recientes primero, hasta el tope.</param>
public record VisitasDelDiaDto(
    DateOnly Fecha,
    int TotalVisitas,
    int TotalPersonas,
    bool Truncada,
    IReadOnlyList<VisitaListadoDto> Visitas);
