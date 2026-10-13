using GIRA.Application.Common;
using GIRA.Application.DTOs.Seguridad;

namespace GIRA.Application.Interfaces.Services.Seguridad;

/// <summary>Gestión de las cuentas de usuario del personal.</summary>
public interface IUsuarioService
{
    Task<OperationResult<PagedResult<UsuarioDto>>> ListarAsync(UsuarioFiltroRequest filtro, CancellationToken ct = default);
    Task<OperationResult<UsuarioDto>> ObtenerAsync(Guid id, CancellationToken ct = default);
    Task<OperationResult<UsuarioCreadoDto>> CrearAsync(CrearUsuarioRequest request, CancellationToken ct = default);
    Task<OperationResult<UsuarioDto>> ActualizarAsync(Guid id, ActualizarUsuarioRequest request, CancellationToken ct = default);
}
