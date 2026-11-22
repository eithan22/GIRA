using GIRA.Application.DTOs.Analitica;
using GIRA.Domain.Entities.Analitica;

namespace GIRA.Application.Interfaces.Repository.Analitica;

public interface IRegistroAfluenciaRepository
{
    Task RegistrarAsync(RegistroAfluencia registro, CancellationToken cancellationToken);

    Task<IReadOnlyList<HistorialAfluenciaDto>> ConsultarAsync(
        DateOnly desde, DateOnly hasta, CancellationToken cancellationToken);
}
