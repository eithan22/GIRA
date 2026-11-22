using GIRA.Application.DTOs.Analitica;

namespace GIRA.Application.Interfaces.Repository.Analitica;

public interface IPrediccionAfluenciaRepository
{
    Task<IReadOnlyList<PrediccionAfluenciaDto>> ConsultarAsync(
        DateOnly desde, DateOnly hasta, Guid? franjaId, CancellationToken cancellationToken);
}
