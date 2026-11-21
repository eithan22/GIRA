using GIRA.Domain.Entities.Inventario;

namespace GIRA.Application.Interfaces.Repository.Inventario;

/// <summary>
/// Consumo proyectado guardado (único por fecha e insumo). Los repositorios de Inventario comparten el
/// mismo contexto: <see cref="GuardarCambiosAsync"/> de cualquiera de ellos guarda todo lo pendiente
/// en una sola transacción.
/// </summary>
public interface IConsumoProyectadoRepository
{
    /// <summary>Filas guardadas entre esas fechas (con seguimiento, para actualizarlas).</summary>
    Task<IReadOnlyList<ConsumoProyectado>> ListarAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken);
    /// <summary>Agrega una fila.</summary>
    void Agregar(ConsumoProyectado consumo);
    /// <summary>Predicciones guardadas entre esas fechas (con seguimiento, para actualizarlas).</summary>
    Task<IReadOnlyList<AfluenciaPredicha>> ListarPrediccionesAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken);
    /// <summary>Agrega una predicción.</summary>
    void AgregarPrediccion(AfluenciaPredicha prediccion);
    /// <summary>Guarda. Lanza InventarioDuplicadoException o InventarioConflictoConcurrenciaException según el caso.</summary>
    Task GuardarCambiosAsync(CancellationToken cancellationToken);
}
