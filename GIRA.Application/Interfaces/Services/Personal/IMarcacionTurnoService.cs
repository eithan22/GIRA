using GIRA.Application.Common;
using GIRA.Application.DTOs.Personal;

namespace GIRA.Application.Interfaces.Services.Personal;

public interface IMarcacionTurnoService
{
    Task<MarcacionTurnoDto?> ObtenerAbiertaAsync(Guid usuarioId, CancellationToken cancellationToken);
    Task<OperationResult<MarcacionTurnoDto>> MarcarEntradaAsync(Guid usuarioId, CancellationToken cancellationToken);
    Task<OperationResult<MarcacionTurnoDto>> MarcarSalidaAsync(
        Guid usuarioId, int minutosPausa, CancellationToken cancellationToken);
}
