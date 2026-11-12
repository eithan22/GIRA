using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Seguridad.Usuarios;
using GIRA.Application.Interfaces.Services.Seguridad.Usuarios;
using GIRA.Infrastructure.Identity;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Data;
using RolesConst = GIRA.Domain.Constants.Roles;

namespace GIRA.Infrastructure.Services.Seguridad.Usuarios;

/// <summary>Implementación de la gestión de usuarios sobre ASP.NET Core Identity.</summary>
public class UsuarioService : IUsuarioService
{
    private readonly UserManager<Usuario> _userManager;
    private readonly GiraDbContext _db;
    private readonly IValidator<CrearUsuarioRequest> _crearValidator;
    private readonly IValidator<ActualizarUsuarioRequest> _actualizarValidator;
    private readonly IValidator<UsuarioFiltroRequest> _filtroValidator;
    private readonly IValidator<AsignarRolesRequest> _asignarRolesValidator;

    public UsuarioService(
        UserManager<Usuario> userManager,
        GiraDbContext db,
        IValidator<CrearUsuarioRequest> crearValidator,
        IValidator<ActualizarUsuarioRequest> actualizarValidator,
        IValidator<UsuarioFiltroRequest> filtroValidator,
        IValidator<AsignarRolesRequest> asignarRolesValidator)
    {
        _userManager = userManager;
        _db = db;
        _crearValidator = crearValidator;
        _actualizarValidator = actualizarValidator;
        _filtroValidator = filtroValidator;
        _asignarRolesValidator = asignarRolesValidator;
    }

    public async Task<OperationResult<PagedResult<UsuarioDto>>> ListarAsync(UsuarioFiltroRequest filtro, CancellationToken ct = default)
    {
        var validacion = await _filtroValidator.ValidateAsync(filtro, ct);
        if (!validacion.IsValid)
            return OperationResult.Failure<PagedResult<UsuarioDto>>(ErrorType.Validation, Mensaje(validacion));

        var query = _db.Users.AsNoTracking();

        if (filtro.Activo.HasValue)
            query = query.Where(u => u.Activo == filtro.Activo.Value);

        if (!string.IsNullOrWhiteSpace(filtro.Rol))
        {
            var rol = filtro.Rol.Trim();
            query = query.Where(u => _db.UserRoles.Any(ur =>
                ur.UserId == u.Id &&
                _db.Roles.Any(r => r.Id == ur.RoleId && r.Name == rol)));
        }

        var total = await query.CountAsync(ct);

        var usuarios = await query
            .OrderBy(u => u.NombreCompleto)
            .Skip((filtro.Page - 1) * filtro.PageSize)
            .Take(filtro.PageSize)
            .ToListAsync(ct);

        var ids = usuarios.Select(u => u.Id).ToList();

        var rolesPorUsuario = await (
            from ur in _db.UserRoles
            join r in _db.Roles on ur.RoleId equals r.Id
            where ids.Contains(ur.UserId)
            select new { ur.UserId, r.Name })
            .ToListAsync(ct);

        var items = usuarios
            .Select(u => ADto(u, rolesPorUsuario
                .Where(x => x.UserId == u.Id)
                .Select(x => x.Name!)
                .ToList()))
            .ToList();

        return OperationResult.Success(new PagedResult<UsuarioDto>(items, total, filtro.Page, filtro.PageSize));
    }

    public async Task<OperationResult<UsuarioDto>> ObtenerAsync(Guid id, CancellationToken ct = default)
    {
        var usuario = await _userManager.FindByIdAsync(id.ToString());
        if (usuario is null)
            return OperationResult.Failure<UsuarioDto>(ErrorType.NotFound, MensajesSeguridad.UsuarioNoExiste);

        var roles = await _userManager.GetRolesAsync(usuario);
        return OperationResult.Success(ADto(usuario, roles.ToList()));
    }

    public async Task<OperationResult<UsuarioCreadoDto>> CrearAsync(CrearUsuarioRequest request, CancellationToken ct = default)
    {
        var validacion = await _crearValidator.ValidateAsync(request, ct);
        if (!validacion.IsValid)
            return OperationResult.Failure<UsuarioCreadoDto>(ErrorType.Validation, Mensaje(validacion));

        var email = request.Email.Trim();

        if (await _userManager.FindByEmailAsync(email) is not null)
            return OperationResult.Failure<UsuarioCreadoDto>(ErrorType.Conflict, "Ya existe un usuario con ese correo.");

        if (request.EmpleadoId.HasValue &&
            await EmpleadoYaVinculadoAsync(request.EmpleadoId.Value, excluirUsuarioId: null, ct))
            return OperationResult.Failure<UsuarioCreadoDto>(ErrorType.Conflict,
                "El empleado ya está vinculado a otro usuario.");

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            NombreCompleto = request.NombreCompleto.Trim(),
            EmpleadoId = request.EmpleadoId,
            Activo = true,
            DebeCambiarPassword = true,
            FechaCreacion = DateTime.UtcNow
        };

        var passwordTemporal = GeneradorPasswordTemporal.Generar();
        var resultado = await _userManager.CreateAsync(usuario, passwordTemporal);

        if (!resultado.Succeeded)
            return OperationResult.Failure<UsuarioCreadoDto>(ErrorType.Validation, Mensaje(resultado));

        var dto = ADto(usuario, new List<string>());
        return OperationResult.Success(new UsuarioCreadoDto(dto, passwordTemporal));
    }

    public async Task<OperationResult<UsuarioDto>> ActualizarAsync(Guid id, ActualizarUsuarioRequest request, CancellationToken ct = default)
    {
        var validacion = await _actualizarValidator.ValidateAsync(request, ct);
        if (!validacion.IsValid)
            return OperationResult.Failure<UsuarioDto>(ErrorType.Validation, Mensaje(validacion));

        var usuario = await _userManager.FindByIdAsync(id.ToString());
        if (usuario is null)
            return OperationResult.Failure<UsuarioDto>(ErrorType.NotFound, MensajesSeguridad.UsuarioNoExiste);

        var email = request.Email.Trim();

        var otro = await _userManager.FindByEmailAsync(email);
        if (otro is not null && otro.Id != usuario.Id)
            return OperationResult.Failure<UsuarioDto>(ErrorType.Conflict, "Ya existe un usuario con ese correo.");

        if (request.EmpleadoId.HasValue &&
            await EmpleadoYaVinculadoAsync(request.EmpleadoId.Value, excluirUsuarioId: id, ct))
            return OperationResult.Failure<UsuarioDto>(ErrorType.Conflict,
                "El empleado ya está vinculado a otro usuario.");

        var rEmail = await _userManager.SetEmailAsync(usuario, email);
        if (!rEmail.Succeeded)
            return OperationResult.Failure<UsuarioDto>(ErrorType.Validation, Mensaje(rEmail));

        var rUserName = await _userManager.SetUserNameAsync(usuario, email);
        if (!rUserName.Succeeded)
            return OperationResult.Failure<UsuarioDto>(ErrorType.Validation, Mensaje(rUserName));

        usuario.NombreCompleto = request.NombreCompleto.Trim();
        usuario.EmpleadoId = request.EmpleadoId;

        var rUpdate = await _userManager.UpdateAsync(usuario);
        if (!rUpdate.Succeeded)
            return OperationResult.Failure<UsuarioDto>(ErrorType.Validation, Mensaje(rUpdate));

        var roles = await _userManager.GetRolesAsync(usuario);
        return OperationResult.Success(ADto(usuario, roles.ToList()));
    }

    public async Task<OperationResult> CambiarEstadoAsync(Guid id, bool activo, CancellationToken ct = default)
    {
        var usuario = await _userManager.FindByIdAsync(id.ToString());
        if (usuario is null)
            return OperationResult.Failure(ErrorType.NotFound, MensajesSeguridad.UsuarioNoExiste);

        if (usuario.Activo == activo)
            return OperationResult.Success();

        // GI-132: no se puede desactivar al último Administrador activo.
        if (!activo && await EsUltimoAdministradorActivoAsync(id, ct))
            return OperationResult.Failure(ErrorType.Conflict,
                "No se puede desactivar al último Administrador activo.");

        usuario.Activo = activo;
        var resultado = await _userManager.UpdateAsync(usuario);
        if (!resultado.Succeeded)
            return OperationResult.Failure(ErrorType.Validation, Mensaje(resultado));

        // GI-131: al desactivar se revocan todos sus refresh tokens vigentes.
        if (!activo)
        {
            var ahora = DateTime.UtcNow;
            var tokens = await _db.RefreshTokens
                .Where(t => t.UsuarioId == id && t.FechaRevocacion == null && t.FechaExpiracion > ahora)
                .ToListAsync(ct);

            foreach (var token in tokens)
                token.FechaRevocacion = ahora;

            await _db.SaveChangesAsync(ct);
        }

        return OperationResult.Success();
    }

    public async Task<OperationResult> EliminarAsync(Guid id, Guid administradorId, CancellationToken ct = default)
    {
        if (id == administradorId)
            return OperationResult.Failure(ErrorType.Conflict, "No puedes eliminar tu propia cuenta.");

        await using var transaccion = await _db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var administradorValido = await (
            from ur in _db.UserRoles
            join rol in _db.Roles on ur.RoleId equals rol.Id
            join u in _db.Users on ur.UserId equals u.Id
            where u.Id == administradorId && u.Activo &&
                  rol.NormalizedName == RolesConst.Administrador.ToUpperInvariant()
            select u.Id).AnyAsync(ct);
        if (!administradorValido)
            return OperationResult.Failure(ErrorType.Unauthorized, "La cuenta no tiene permisos de administrador activos.");

        var usuario = await _userManager.FindByIdAsync(id.ToString());
        if (usuario is null)
            return OperationResult.Failure(ErrorType.NotFound, MensajesSeguridad.UsuarioNoExiste);

        var administradores = await (
            from ur in _db.UserRoles
            join rol in _db.Roles on ur.RoleId equals rol.Id
            join u in _db.Users on ur.UserId equals u.Id
            where rol.NormalizedName == RolesConst.Administrador.ToUpperInvariant()
            select new { u.Id, u.Activo })
            .ToListAsync(ct);

        if (administradores.Any(a => a.Id == id) &&
            (administradores.Count == 1 ||
             (usuario.Activo && administradores.Count(a => a.Activo) == 1)))
            return OperationResult.Failure(ErrorType.Conflict, "No se puede eliminar al último Administrador del sistema o al último Administrador activo.");

        var ahora = DateTime.UtcNow;
        var tokens = await _db.RefreshTokens
            .Where(t => t.UsuarioId == id && t.FechaRevocacion == null)
            .ToListAsync(ct);
        foreach (var token in tokens)
            token.FechaRevocacion = ahora;

        var resultado = await _userManager.DeleteAsync(usuario);
        if (!resultado.Succeeded)
            return OperationResult.Failure(ErrorType.Conflict, Mensaje(resultado));

        await _db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);
        return OperationResult.Success();
    }

    public async Task<bool> EsUltimoAdministradorActivoAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var admins = await (
            from ur in _db.UserRoles
            join r in _db.Roles on ur.RoleId equals r.Id
            join u in _db.Users on ur.UserId equals u.Id
            where r.Name == RolesConst.Administrador && u.Activo
            select u.Id)
            .ToListAsync(ct);

        return admins.Count == 1 && admins[0] == usuarioId;
    }

    public async Task<OperationResult<UsuarioDto>> AsignarRolesAsync(Guid id, AsignarRolesRequest request, CancellationToken ct = default)
    {
        var validacion = await _asignarRolesValidator.ValidateAsync(request, ct);
        if (!validacion.IsValid)
            return OperationResult.Failure<UsuarioDto>(ErrorType.Validation, Mensaje(validacion));

        var usuario = await _userManager.FindByIdAsync(id.ToString());
        if (usuario is null)
            return OperationResult.Failure<UsuarioDto>(ErrorType.NotFound, MensajesSeguridad.UsuarioNoExiste);

        // Solo se aceptan roles existentes; los nombres se comparan sin distinguir mayúsculas.
        var pedidos = request.Roles.Select(r => r.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        var normalizados = pedidos.Select(r => r.ToUpperInvariant()).ToList();
        var rolesDestino = await _db.Roles
            .Where(r => normalizados.Contains(r.NormalizedName!))
            .ToListAsync(ct);

        var inexistentes = pedidos
            .Where(p => rolesDestino.All(r => r.NormalizedName != p.ToUpperInvariant()))
            .ToList();
        if (inexistentes.Count > 0)
            return OperationResult.Failure<UsuarioDto>(ErrorType.Validation,
                $"Estos roles no existen: {string.Join(", ", inexistentes)}.");

        // RN-SEG-06: no se puede quitar el rol Administrador al último Administrador activo.
        var rolesActuales = await _userManager.GetRolesAsync(usuario);
        var quitaAdministrador = rolesActuales.Contains(RolesConst.Administrador)
            && rolesDestino.All(r => r.Name != RolesConst.Administrador);
        if (quitaAdministrador && await EsUltimoAdministradorActivoAsync(id, ct))
            return OperationResult.Failure<UsuarioDto>(ErrorType.Conflict,
                "No se puede quitar el rol Administrador al último Administrador activo.");

        // Quitar y agregar en un solo SaveChanges para que el reemplazo sea atómico.
        var idsDestino = rolesDestino.Select(r => r.Id).ToHashSet();
        var asignaciones = await _db.UserRoles.Where(ur => ur.UserId == id).ToListAsync(ct);
        _db.UserRoles.RemoveRange(asignaciones.Where(ur => !idsDestino.Contains(ur.RoleId)));
        _db.UserRoles.AddRange(idsDestino
            .Where(rolId => asignaciones.All(ur => ur.RoleId != rolId))
            .Select(rolId => new IdentityUserRole<Guid> { UserId = id, RoleId = rolId }));
        await _db.SaveChangesAsync(ct);

        return OperationResult.Success(ADto(usuario, rolesDestino.Select(r => r.Name!).Order().ToList()));
    }

    public async Task<OperationResult<PasswordReseteadaDto>> ResetearPasswordAsync(Guid id, CancellationToken ct = default)
    {
        var usuario = await _userManager.FindByIdAsync(id.ToString());
        if (usuario is null)
            return OperationResult.Failure<PasswordReseteadaDto>(ErrorType.NotFound, MensajesSeguridad.UsuarioNoExiste);

        var roles = await _userManager.GetRolesAsync(usuario);
        var passwordTemporal = GeneradorPasswordTemporal.Generar();

        // Identity, los refresh tokens y las solicitudes usan el mismo GiraDbContext.
        // Serializable es intencional y no debe bajarse sin analizar la carrera (igual que en
        // AuthService.CambiarPasswordAsync): evita que un refresh concurrente cree una sesión
        // nueva que sobreviva al reseteo de la contraseña.
        await using var transaccion = await _db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            ct);

        // Quitar y volver a poner la contraseña es el mecanismo de Identity para que un
        // Administrador fije una nueva sin conocer la anterior.
        var quitar = await _userManager.RemovePasswordAsync(usuario);
        if (!quitar.Succeeded)
            return OperationResult.Failure<PasswordReseteadaDto>(ErrorType.Validation, Mensaje(quitar));

        // Se marca antes de AddPasswordAsync para que Identity lo guarde en la misma
        // actualización que el hash de la contraseña.
        usuario.DebeCambiarPassword = true;

        var poner = await _userManager.AddPasswordAsync(usuario, passwordTemporal);
        if (!poner.Succeeded)
            return OperationResult.Failure<PasswordReseteadaDto>(ErrorType.Validation, Mensaje(poner));

        // Las sesiones abiertas con la contraseña anterior dejan de servir.
        var ahora = DateTime.UtcNow;
        var tokens = await _db.RefreshTokens
            .Where(t => t.UsuarioId == id && t.FechaRevocacion == null)
            .ToListAsync(ct);

        foreach (var token in tokens)
            token.FechaRevocacion = ahora;

        // La solicitud de recuperación queda atendida; se borra para sacarla de la cola.
        var solicitud = await _db.SolicitudesRecuperacionPassword
            .FirstOrDefaultAsync(s => s.UsuarioId == id, ct);

        if (solicitud is not null)
            _db.SolicitudesRecuperacionPassword.Remove(solicitud);

        await _db.SaveChangesAsync(ct);
        await transaccion.CommitAsync(ct);

        return OperationResult.Success(
            new PasswordReseteadaDto(ADto(usuario, roles.ToList()), passwordTemporal));
    }

    public async Task<OperationResult<IReadOnlyList<SolicitudRecuperacionDto>>> ListarSolicitudesRecuperacionAsync(CancellationToken ct = default)
    {
        // La más antigua primero: el Administrador atiende la cola en orden de llegada.
        var solicitudes = await (
            from s in _db.SolicitudesRecuperacionPassword.AsNoTracking()
            join u in _db.Users on s.UsuarioId equals u.Id
            orderby s.FechaSolicitud
            select new { s.UsuarioId, u.NombreCompleto, u.Email, s.FechaSolicitud })
            .ToListAsync(ct);

        // SQL Server devuelve las fechas sin zona (Kind = Unspecified); se marcan como UTC
        // para que el JSON salga con "Z" y el cliente no las confunda con hora local.
        IReadOnlyList<SolicitudRecuperacionDto> items = solicitudes
            .Select(s => new SolicitudRecuperacionDto(
                s.UsuarioId,
                s.NombreCompleto,
                s.Email ?? string.Empty,
                DateTime.SpecifyKind(s.FechaSolicitud, DateTimeKind.Utc)))
            .ToList();

        return OperationResult.Success(items);
    }

    public async Task<OperationResult> DescartarSolicitudRecuperacionAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var solicitud = await _db.SolicitudesRecuperacionPassword
            .FirstOrDefaultAsync(s => s.UsuarioId == usuarioId, ct);

        // 404 en vez de éxito silencioso: si la solicitud ya no está, el Administrador debe
        // saber que otro la atendió antes (la lista que tenía en pantalla está vieja).
        if (solicitud is null)
            return OperationResult.Failure(ErrorType.NotFound,
                "No hay una solicitud de recuperación pendiente para ese usuario.");

        // No se toca la contraseña: descartar solo saca la solicitud de la cola.
        _db.SolicitudesRecuperacionPassword.Remove(solicitud);
        await _db.SaveChangesAsync(ct);

        return OperationResult.Success();
    }

    public async Task<OperationResult<UsuarioActualDto>> ObtenerActualAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var usuario = await _userManager.FindByIdAsync(usuarioId.ToString());
        if (usuario is null || !usuario.Activo)
            return OperationResult.Failure<UsuarioActualDto>(ErrorType.Unauthorized, MensajesSeguridad.UsuarioNoAutorizado);

        var roles = await _userManager.GetRolesAsync(usuario);

        return OperationResult.Success(new UsuarioActualDto(
            usuario.Id,
            usuario.Email ?? string.Empty,
            usuario.NombreCompleto,
            roles.ToList(),
            usuario.EmpleadoId,
            usuario.DebeCambiarPassword));

    }
    private static UsuarioDto ADto(Usuario u, IReadOnlyList<string> roles) =>
        new(u.Id, u.Email ?? string.Empty, u.NombreCompleto, u.Activo, u.EmpleadoId, u.UltimoAcceso, roles);

    /// <summary>Indica si el empleado ya está vinculado a otro usuario distinto de <paramref name="excluirUsuarioId"/>.</summary>
    private async Task<bool> EmpleadoYaVinculadoAsync(Guid empleadoId, Guid? excluirUsuarioId, CancellationToken ct) =>
        await _db.Users.AnyAsync(u => u.EmpleadoId == empleadoId && u.Id != excluirUsuarioId, ct);

    private static string Mensaje(FluentValidation.Results.ValidationResult r) =>
        string.Join(" ", r.Errors.Select(e => e.ErrorMessage));

    private static string Mensaje(IdentityResult r) =>
        string.Join(" ", r.Errors.Select(e => e.Description));
}