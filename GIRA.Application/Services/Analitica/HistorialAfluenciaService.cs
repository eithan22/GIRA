using GIRA.Application.DTOs.Analitica;
using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Application.Interfaces.Services.Analitica;

namespace GIRA.Application.Services.Analitica;

public sealed class HistorialAfluenciaService(IRegistroAfluenciaRepository registros) : IHistorialAfluenciaService
{
    public Task<IReadOnlyList<HistorialAfluenciaDto>> ConsultarAsync(
        DateOnly desde, DateOnly hasta, CancellationToken cancellationToken) =>
        registros.ConsultarAsync(desde, hasta, cancellationToken);
}
