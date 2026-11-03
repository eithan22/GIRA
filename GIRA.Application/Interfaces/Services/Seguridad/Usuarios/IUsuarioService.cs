using GIRA.Application.Common;
using GIRA.Application.DTOs.Seguridad.Usuarios;

namespace GIRA.Application.Interfaces.Services.Seguridad.Usuarios;

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

    /// <summary>
    /// GI-218: Resetea la contraseña del usuario. Genera una contraseña temporal,
    /// lo obliga a cambiarla en el próximo inicio de sesión, revoca sus refresh tokens activos
    /// y cierra la solicitud de recuperación pendiente si la había. La contraseña en claro se
    /// devuelve solo en esta respuesta.
    /// </summary>
    Task<OperationResult<PasswordReseteadaDto>> ResetearPasswordAsync(Guid id, CancellationToken ct = default);

    /// <summary>
    /// GI-219: Lista las solicitudes de recuperación de contraseña pendientes, de la más
    /// antigua a la más reciente (orden de atención).
    /// </summary>
    Task<OperationResult<IReadOnlyList<SolicitudRecuperacionDto>>> ListarSolicitudesRecuperacionAsync(CancellationToken ct = default);

    /// <summary>
    /// GI-219: Descarta la solicitud de recuperación de un usuario sin tocar su contraseña
    /// (por ejemplo, cuando el usuario recordó la suya o la solicitud no era legítima).
    /// </summary>
    Task<OperationResult> DescartarSolicitudRecuperacionAsync(Guid usuarioId, CancellationToken ct = default);
}

