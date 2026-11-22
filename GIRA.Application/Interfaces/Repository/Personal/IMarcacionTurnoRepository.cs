using GIRA.Domain.Entities.Personal;

namespace GIRA.Application.Interfaces.Repository.Personal;

public interface IMarcacionTurnoRepository
{
    Task<MarcacionTurno?> ObtenerAbiertaAsync(Guid usuarioId, CancellationToken cancellationToken);
    Task AgregarAsync(MarcacionTurno marcacion, CancellationToken cancellationToken);
    Task GuardarAsync(CancellationToken cancellationToken);
}
