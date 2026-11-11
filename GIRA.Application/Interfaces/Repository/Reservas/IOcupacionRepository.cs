using GIRA.Domain.Enums.Reservas;

namespace GIRA.Application.Interfaces.Repository.Reservas;

/// <summary>Franja tal como la necesita el mapa de ocupación (sin seguimiento de cambios).</summary>
public sealed record FranjaOcupacionLectura(Guid Id, string Nombre, TimeOnly HoraInicio, TimeOnly HoraFin);

/// <summary>Zona del mapa de ocupación, activa o no.</summary>
public sealed record ZonaOcupacionLectura(Guid Id, string Nombre, bool Activa);

/// <summary>Mesa del mapa de ocupación, activa o no.</summary>
public sealed record MesaOcupacionLectura(Guid Id, Guid ZonaId, int Numero, int Capacidad, bool Activa);

/// <summary>Reserva vigente (Confirmada o EnCurso) de una mesa, con el nombre del cliente y sin su teléfono.</summary>
public sealed record ReservaVigenteLectura(
    Guid MesaId, Guid ReservaId, EstadoReserva Estado, string ClienteNombre, int Personas, int? PersonasReales);

/// <summary>
/// Lecturas del mapa de ocupación (GI-167). Solo lectura: sin seguimiento de cambios, con proyección
/// directa (sin cargar entidades ni el teléfono del cliente), respetando el borrado lógico.
/// </summary>
public interface IOcupacionRepository
{
    /// <summary>La franja (activa o no); null si no existe.</summary>
    Task<FranjaOcupacionLectura?> ObtenerFranjaAsync(Guid franjaId, CancellationToken cancellationToken);

    /// <summary>Todas las zonas, incluidas las inactivas.</summary>
    Task<IReadOnlyList<ZonaOcupacionLectura>> ListarZonasAsync(CancellationToken cancellationToken);

    /// <summary>Todas las mesas, incluidas las inactivas.</summary>
    Task<IReadOnlyList<MesaOcupacionLectura>> ListarMesasAsync(CancellationToken cancellationToken);

    /// <summary>Reservas Confirmada o EnCurso de esa fecha y franja.</summary>
    Task<IReadOnlyList<ReservaVigenteLectura>> ListarReservasVigentesAsync(DateOnly fecha, Guid franjaId, CancellationToken cancellationToken);
}
