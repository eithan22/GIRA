using GIRA.Application.DTOs.Analitica;

namespace GIRA.Application.Interfaces.Services.Analitica;

public interface IHistorialAfluenciaService
{
    Task<IReadOnlyList<HistorialAfluenciaDto>> ConsultarAsync(
        DateOnly desde, DateOnly hasta, CancellationToken cancellationToken);
}
