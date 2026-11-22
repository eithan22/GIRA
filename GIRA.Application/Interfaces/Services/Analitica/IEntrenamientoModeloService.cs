using GIRA.Application.DTOs.Analitica;

namespace GIRA.Application.Interfaces.Services.Analitica;

public interface IEntrenamientoModeloService
{
    Task<Guid> SolicitarAsync(Guid usuarioId, CancellationToken cancellationToken);
    Task<EntrenamientoModeloDto?> ConsultarAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyList<ModeloAfluenciaDto>> ListarModelosAsync(CancellationToken cancellationToken);
    Task<ResultadoActivacionModelo> ActivarModeloAsync(
        Guid modeloId, Guid usuarioId, string motivo, CancellationToken cancellationToken);
    Task<bool> ProcesarSiguienteAsync(CancellationToken cancellationToken);
}
