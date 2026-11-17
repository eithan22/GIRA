namespace GIRA.Application.DTOs.Reservas;

/// <summary>Fila de la agenda del personal (GI-164).</summary>
/// <remarks>
/// <see cref="Estado"/> y <see cref="Origen"/> van como texto, no como número: el proyecto no
/// registra <c>JsonStringEnumConverter</c>, así que se resuelve local a este DTO (mismo criterio
/// que <see cref="ReservaConsultadaDto"/>), sin tocar la configuración global.
/// </remarks>
public record ReservaListadoDto(
    Guid Id,
    string Codigo,
    string Estado,
    DateOnly Fecha,
    Guid FranjaId,
    string FranjaNombre,
    TimeOnly FranjaHoraInicio,
    TimeOnly FranjaHoraFin,
    int MesaNumero,
    string ZonaNombre,
    int Personas,
    int? PersonasReales,
    string ClienteNombre,
    string ClienteTelefono,
    string Origen);
