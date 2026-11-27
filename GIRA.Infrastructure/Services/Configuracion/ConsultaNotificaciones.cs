using GIRA.Application.DTOs.Configuracion;
using GIRA.Application.Interfaces.Services.Configuracion;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Services.Configuracion;

/// <summary>
/// Implementación de <see cref="IConsultaNotificaciones"/>: lee cuentas y roles de Identity, y los nombres de insumos,
/// franjas y cargos, solo para armar el texto de los avisos. Todo de solo lectura.
/// </summary>
public class ConsultaNotificaciones : IConsultaNotificaciones
{
    private readonly GiraDbContext _contexto;

    /// <summary>Crea la consulta con el contexto de EF Core.</summary>
    public ConsultaNotificaciones(GiraDbContext contexto)
    {
        _contexto = contexto;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<DestinatarioNotificacion>> UsuariosConRolesAsync(
        IReadOnlyCollection<string> roles, CancellationToken cancellationToken)
    {
        var filas = await (
                from usuario in _contexto.Users.AsNoTracking()
                join usuarioRol in _contexto.UserRoles on usuario.Id equals usuarioRol.UserId
                join rol in _contexto.Roles on usuarioRol.RoleId equals rol.Id
                where usuario.Activo && roles.Contains(rol.Name!)
                select new { usuario.Id, Rol = rol.Name! })
            .ToListAsync(cancellationToken);

        // Un usuario con varios roles recibe un solo aviso, con el rol que aparece primero en la lista pedida.
        return filas
            .GroupBy(f => f.Id)
            .Select(g => new DestinatarioNotificacion(g.Key, roles.First(r => g.Any(f => f.Rol == r))))
            .ToList();
    }

    /// <inheritdoc />
    public Task<Guid?> UsuarioDelEmpleadoAsync(Guid empleadoId, CancellationToken cancellationToken) =>
        _contexto.Users.AsNoTracking()
            .Where(u => u.EmpleadoId == empleadoId && u.Activo)
            .Select(u => (Guid?)u.Id)
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public Task<InsumoAviso?> InsumoAsync(Guid insumoId, CancellationToken cancellationToken) =>
        _contexto.Insumos.AsNoTracking()
            .Where(i => i.Id == insumoId)
            .Select(i => new InsumoAviso(i.Nombre, i.UnidadMedida != null ? i.UnidadMedida.Abreviatura : string.Empty))
            .FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public Task<string?> NombreFranjaAsync(Guid franjaId, CancellationToken cancellationToken) =>
        _contexto.FranjasHorarias.AsNoTracking().Where(f => f.Id == franjaId).Select(f => f.Nombre).FirstOrDefaultAsync(cancellationToken);

    /// <inheritdoc />
    public Task<string?> NombreCargoAsync(Guid cargoId, CancellationToken cancellationToken) =>
        _contexto.Cargos.AsNoTracking().Where(c => c.Id == cargoId).Select(c => c.Nombre).FirstOrDefaultAsync(cancellationToken);
}
