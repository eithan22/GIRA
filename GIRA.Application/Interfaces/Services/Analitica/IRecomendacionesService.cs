using GIRA.Application.DTOs.Analitica;

namespace GIRA.Application.Interfaces.Services.Analitica;

public interface IRecomendacionesService
{
    Task ActualizarAsync(CancellationToken ct);
    Task<IReadOnlyList<RecomendacionDto>> ConsultarAsync(string? estado, CancellationToken ct);
    Task<bool> CambiarEstadoAsync(Guid id, string estado, CancellationToken ct);
}
