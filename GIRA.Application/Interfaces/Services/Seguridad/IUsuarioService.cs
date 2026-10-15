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
    Task<OperationResult> CambiarEstadoAsync(Guid id, bool activo, CancellationToken ct = default);
    Task<OperationResult<UsuarioActualDto>> ObtenerActualAsync(Guid usuarioId, CancellationToken ct = default);

    /// <summary>Indica si el usuario es el único Administrador activo (GI-132). Reutilizable por GI-99.</summary>
    Task<bool> EsUltimoAdministradorActivoAsync(Guid usuarioId, CancellationToken ct = default);

    /// <summary>
    /// Reemplaza los roles del usuario por los indicados (GI-99). Solo acepta roles existentes,
    /// exige al menos uno y no quita el rol Administrador al último Administrador activo.
    /// </summary>
    Task<OperationResult<UsuarioDto>> AsignarRolesAsync(Guid id, AsignarRolesRequest request, CancellationToken ct = default);
}

