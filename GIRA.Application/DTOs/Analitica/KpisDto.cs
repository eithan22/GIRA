namespace GIRA.Application.DTOs.Analitica;

/// <summary>Promedio de afluencia para un día de la semana y franja, usando solo fechas observadas.</summary>
public sealed record AfluenciaPromedioDto(
    DayOfWeek DiaSemana, Guid FranjaHorariaId, string Franja, decimal Promedio, int FechasObservadas);

/// <summary>Indicadores del período solicitado y alertas abiertas al momento de la consulta.</summary>
public sealed record KpisDto(
    DateOnly Desde,
    DateOnly Hasta,
    int? ComensalesAtendidos,
    decimal? TasaOcupacion,
    decimal? TasaNoShow,
    decimal? TasaCancelacion,
    IReadOnlyList<AfluenciaPromedioDto> AfluenciaPromedio,
    decimal? ProporcionWalkIns,
    decimal? PrecisionModelo,
    decimal? CostoInsumosPorComensal,
    decimal? Merma,
    int AlertasAbiertas,
    decimal? HorasPersonalPorComensal);
