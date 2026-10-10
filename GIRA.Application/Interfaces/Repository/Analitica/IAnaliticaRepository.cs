using GIRA.Domain.Entities.Analitica;

namespace GIRA.Application.Interfaces.Repository.Analitica;

/// <summary>Operaciones de persistencia del módulo de Analítica.</summary>
public interface IAnaliticaRepository
{
    /// <summary>Comprueba si ya existe un registro de historial para una reserva.</summary>
    Task<bool> ExisteReservaAsync(Guid reservaId, CancellationToken cancellationToken);

    /// <summary>Persiste un nuevo registro de afluencia.</summary>
    Task AgregarHistorialAsync(HistorialAfluencia registro, CancellationToken cancellationToken);

    /// <summary>Obtiene registros de afluencia dentro de un rango inclusivo de fechas.</summary>
    Task<IReadOnlyList<HistorialAfluencia>> ObtenerHistorialAsync(
        DateOnly desde,
        DateOnly hasta,
        CancellationToken cancellationToken);

    /// <summary>Obtiene predicciones de una fecha concreta.</summary>
    Task<IReadOnlyList<PrediccionResultado>> ObtenerPrediccionesAsync(
        DateOnly fecha,
        CancellationToken cancellationToken);

    /// <summary>Reemplaza las predicciones de un rango de fechas en una sola operación.</summary>
    Task ReemplazarPrediccionesAsync(
        DateOnly desde,
        DateOnly hasta,
        IReadOnlyCollection<PrediccionResultado> predicciones,
        CancellationToken cancellationToken);
}
