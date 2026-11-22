using GIRA.Application.Events.Analitica;
using GIRA.Domain.Entities.Analitica;

namespace GIRA.Application.Interfaces.Repository.Analitica;

public sealed record PublicacionPendiente(Guid Id, PrediccionAfluenciaGeneradaEvent Evento);

public interface IGeneracionPrediccionRepository
{
    Task<IReadOnlyList<Guid>> ObtenerFranjasActivasAsync(CancellationToken cancellationToken);
    Task<bool> ExisteAsync(DateOnly fechaProceso, CancellationToken cancellationToken);
    Task GuardarAsync(GeneracionPrediccion generacion, IReadOnlyList<PrediccionAfluencia> predicciones,
        IReadOnlyList<PublicacionPrediccion> publicaciones, CancellationToken cancellationToken);
    Task<IReadOnlyList<PublicacionPendiente>> ObtenerPendientesAsync(CancellationToken cancellationToken);
    Task MarcarPublicadaAsync(Guid id, DateTimeOffset fecha, CancellationToken cancellationToken);
}
