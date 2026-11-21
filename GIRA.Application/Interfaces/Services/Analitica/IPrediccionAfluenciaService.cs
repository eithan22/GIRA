using GIRA.Application.DTOs.Analitica;

namespace GIRA.Application.Interfaces.Services.Analitica;

public interface IPrediccionAfluenciaService
{
    Task<IReadOnlyList<PrediccionAfluenciaDto>> ConsultarAsync(
        DateOnly desde, DateOnly hasta, Guid? franjaId, CancellationToken cancellationToken);
}
