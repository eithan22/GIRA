using GIRA.Application.DTOs.Configuracion;

namespace GIRA.Application.Interfaces.Services.Configuracion;

/// <summary>Insumo tal como lo nombra un aviso.</summary>
public sealed record InsumoAviso(string Nombre, string Unidad);

/// <summary>
/// Puerto de lectura para armar los avisos: a quién mandarlos y los nombres que los eventos traen solo como Id.
/// Lo implementa Infrastructure, que lee las cuentas de Identity y los catálogos sin que Configuración use esas entidades.
/// </summary>
public interface IConsultaNotificaciones
{
    /// <summary>Usuarios activos con alguno de los roles; cada uno una sola vez, con el primero de sus roles que coincida.</summary>
    Task<IReadOnlyList<DestinatarioNotificacion>> UsuariosConRolesAsync(IReadOnlyCollection<string> roles, CancellationToken cancellationToken);

    /// <summary>Usuario activo vinculado al empleado (<c>Usuario.EmpleadoId</c>); null si el empleado no tiene cuenta.</summary>
    Task<Guid?> UsuarioDelEmpleadoAsync(Guid empleadoId, CancellationToken cancellationToken);

    /// <summary>Nombre y unidad del insumo; null si no existe.</summary>
    Task<InsumoAviso?> InsumoAsync(Guid insumoId, CancellationToken cancellationToken);

    /// <summary>Nombre de la franja horaria; null si no existe.</summary>
    Task<string?> NombreFranjaAsync(Guid franjaId, CancellationToken cancellationToken);

    /// <summary>Nombre del cargo; null si no existe.</summary>
    Task<string?> NombreCargoAsync(Guid cargoId, CancellationToken cancellationToken);
}
