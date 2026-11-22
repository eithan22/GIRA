namespace GIRA.Application.Interfaces.Repository.Analitica;

/// <summary>Consultas de datos de otros módulos necesarias para los indicadores.</summary>
public interface IKpisRepository
{
    /// <summary>Cuenta las reservas creadas en el intervalo UTC y las que están canceladas actualmente.</summary>
    Task<(int Creadas, int Canceladas)> ContarReservasAsync(
        DateTime desdeUtc, DateTime hastaUtcExclusivo, CancellationToken cancellationToken);

    /// <summary>Cuenta las alertas de inventario actualmente abiertas.</summary>
    Task<int> ContarAlertasAbiertasAsync(CancellationToken cancellationToken);

    /// <summary>Calcula el MAE de los últimos pronósticos hechos antes de cada fecha observada.</summary>
    Task<decimal?> CalcularPrecisionModeloAsync(
        DateOnly desde, DateOnly hasta, CancellationToken cancellationToken);

    Task<(int DiasRegistrados, int CapacidadTotal)> ConsultarCapacidadAsync(
        DateOnly desde, DateOnly hasta, CancellationToken cancellationToken);

    Task<(decimal? CostoSalidas, decimal? CostoMermas)> ConsultarCostosAsync(
        DateTime desdeUtc, DateTime hastaUtcExclusivo, CancellationToken cancellationToken);

    Task<decimal?> ConsultarHorasPersonalAsync(
        DateTime desdeUtc, DateTime hastaUtcExclusivo, CancellationToken cancellationToken);

    /// <summary>Obtiene los nombres actuales de las franjas presentes en el historial.</summary>
    Task<IReadOnlyDictionary<Guid, string>> ObtenerFranjasAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
}
