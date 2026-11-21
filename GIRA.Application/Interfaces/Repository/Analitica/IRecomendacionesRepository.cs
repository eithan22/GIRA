using GIRA.Application.DTOs.Analitica;
using GIRA.Application.Services.Analitica;

namespace GIRA.Application.Interfaces.Repository.Analitica;

public interface IRecomendacionesRepository
{
    Task<IReadOnlyList<AlertaRecurrente>> ContarAlertasAsync(DateTime desdeUtc, DateTime hastaUtc, CancellationToken ct);
    Task<IReadOnlyList<RecomendacionDto>> GuardarVigentesAsync(
        IReadOnlyList<RecomendacionCalculada> candidatas, DateTimeOffset ahora, CancellationToken ct);
    Task<IReadOnlyList<RecomendacionDto>> ConsultarAsync(string? estado, CancellationToken ct);
    Task<bool> CambiarEstadoAsync(Guid id, string estado, CancellationToken ct);
}
