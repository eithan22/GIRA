using GIRA.Application.DTOs.Analitica;
using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Application.Interfaces.Services.Analitica;

namespace GIRA.Application.Services.Analitica;

public sealed class PrediccionAfluenciaService(IPrediccionAfluenciaRepository predicciones) : IPrediccionAfluenciaService
{
    public Task<IReadOnlyList<PrediccionAfluenciaDto>> ConsultarAsync(
        DateOnly desde, DateOnly hasta, Guid? franjaId, CancellationToken cancellationToken) =>
        predicciones.ConsultarAsync(desde, hasta, franjaId, cancellationToken);
}
