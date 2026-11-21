using GIRA.Application.DTOs.Analitica;
using GIRA.Domain.Entities.Analitica;

namespace GIRA.Application.Interfaces.Repository.Analitica;

public interface IModelosAfluenciaRepository : IModelosActivosRepository
{
    Task<Guid> SolicitarEntrenamientoAsync(Guid usuarioId, DateTimeOffset solicitadoEn,
        CancellationToken cancellationToken);
    Task<IReadOnlyList<Guid>> ObtenerFranjasActivasAsync(CancellationToken cancellationToken);
    Task<EntrenamientoModelo?> IniciarSiguienteEntrenamientoAsync(DateTimeOffset iniciadoEn,
        CancellationToken cancellationToken);
    Task ReencolarEntrenamientoAsync(Guid entrenamientoId, CancellationToken cancellationToken);
    Task<EntrenamientoModelo?> ObtenerEntrenamientoAsync(Guid id, CancellationToken cancellationToken);
    Task<IReadOnlyDictionary<Guid, string>> ObtenerNombresFranjasAsync(
        IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);
    Task<IReadOnlyList<ModeloAfluenciaDto>> ObtenerModelosAsync(CancellationToken cancellationToken);
    Task GuardarResultadosAsync(Guid entrenamientoId, IReadOnlyList<ResultadoEntrenamientoModelo> resultados,
        IReadOnlyList<ModeloAfluencia> modelos, DateTimeOffset finalizadoEn, CancellationToken cancellationToken);
    Task MarcarEntrenamientoFallidoAsync(Guid entrenamientoId, string error, DateTimeOffset finalizadoEn,
        CancellationToken cancellationToken);
    Task<ResultadoActivacionModelo> ActivarModeloAsync(Guid modeloId, Guid usuarioId, string motivo, DateTimeOffset activadoEn,
        bool esRollback, CancellationToken cancellationToken);
}
