using GIRA.Application.Common;
using GIRA.Application.DTOs.Seguridad.Accesos;

namespace GIRA.Application.Interfaces.Services.Seguridad;

public interface IHistorialAccesosService
{
    /// <summary>
    /// Devuelve una página del historial de accesos del usuario, del más reciente al más antiguo.
    /// Devuelve <see cref="ErrorType.Validation"/> si la paginación o las fechas son inválidas y
    /// <see cref="ErrorType.NotFound"/> si el usuario no existe.
    /// </summary>
    Task<OperationResult<HistorialAccesosDto>> ListarAsync(Guid usuarioId, HistorialAccesosRequest filtro, CancellationToken cancellationToken = default);
}
