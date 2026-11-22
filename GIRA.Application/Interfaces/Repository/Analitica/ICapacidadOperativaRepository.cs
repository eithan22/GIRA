using GIRA.Domain.Entities.Analitica;

namespace GIRA.Application.Interfaces.Repository.Analitica;

public interface ICapacidadOperativaRepository
{
    Task<bool> ExisteAsync(DateOnly fecha, CancellationToken cancellationToken);
    Task<int> CalcularCapacidadActualAsync(CancellationToken cancellationToken);
    Task GuardarAsync(CapacidadOperativaDiaria capacidad, CancellationToken cancellationToken);
}
