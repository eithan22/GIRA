using GIRA.Domain.Entities.Analitica;

namespace GIRA.Application.Interfaces.Repository.Analitica;

public interface IModelosActivosRepository
{
    Task<IReadOnlyList<ModeloAfluencia>> ObtenerModelosActivosAsync(CancellationToken cancellationToken);
}
