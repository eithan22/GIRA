using System.Security.Cryptography;
using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Seguridad;
using GIRA.Application.Interfaces.Services.Seguridad;
using GIRA.Infrastructure.Identity;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using GIRA.Domain.Constants;

namespace GIRA.Infrastructure.Services.Seguridad;

/// <summary>Implementación de la gestión de usuarios sobre ASP.NET Core Identity.</summary>
public class UsuarioService : IUsuarioService
{
    private readonly UserManager<Usuario> _userManager;
    private readonly GiraDbContext _db;
    private readonly IValidator<CrearUsuarioRequest> _crearValidator;
    private readonly IValidator<ActualizarUsuarioRequest> _actualizarValidator;
    private readonly IValidator<UsuarioFiltroRequest> _filtroValidator;

    public UsuarioService(
        UserManager<Usuario> userManager,
        GiraDbContext db,
        IValidator<CrearUsuarioRequest> crearValidator,
        IValidator<ActualizarUsuarioRequest> actualizarValidator,
        IValidator<UsuarioFiltroRequest> filtroValidator)
    {
        _userManager = userManager;
        _db = db;
        _crearValidator = crearValidator;
        _actualizarValidator = actualizarValidator;
        _filtroValidator = filtroValidator;
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
            return OperationResult.Failure<UsuarioDto>(ErrorType.NotFound, "El usuario no existe.");

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

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            NombreCompleto = request.NombreCompleto.Trim(),
            EmpleadoId = request.EmpleadoId,
            Activo = true,
            FechaCreacion = DateTime.UtcNow
        };

        var passwordTemporal = GenerarPasswordTemporal();
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
            return OperationResult.Failure<UsuarioDto>(ErrorType.NotFound, "El usuario no existe.");

        var email = request.Email.Trim();

        var otro = await _userManager.FindByEmailAsync(email);
        if (otro is not null && otro.Id != usuario.Id)
            return OperationResult.Failure<UsuarioDto>(ErrorType.Conflict, "Ya existe un usuario con ese correo.");

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
            return OperationResult.Failure(ErrorType.NotFound, "El usuario no existe.");

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

    public async Task<bool> EsUltimoAdministradorActivoAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var admins = await (
            from ur in _db.UserRoles
            join r in _db.Roles on ur.RoleId equals r.Id
            join u in _db.Users on ur.UserId equals u.Id
            where r.Name == Roles.Administrador && u.Activo
            select u.Id)
            .ToListAsync(ct);

        return admins.Count == 1 && admins[0] == usuarioId;
    }

    public async Task<OperationResult<UsuarioActualDto>> ObtenerActualAsync(Guid usuarioId, CancellationToken ct = default)
    {
        var usuario = await _userManager.FindByIdAsync(usuarioId.ToString());
        if (usuario is null || !usuario.Activo)
            return OperationResult.Failure<UsuarioActualDto>(ErrorType.Unauthorized, "Usuario no autorizado.");

        var roles = await _userManager.GetRolesAsync(usuario);

        return OperationResult.Success(new UsuarioActualDto(
            usuario.Id,
            usuario.Email ?? string.Empty,
            usuario.NombreCompleto,
            roles.ToList(),
            usuario.EmpleadoId));
    }
    private static UsuarioDto ADto(Usuario u, IReadOnlyList<string> roles) =>
        new(u.Id, u.Email ?? string.Empty, u.NombreCompleto, u.Activo, u.EmpleadoId, u.UltimoAcceso, roles);

    private static string Mensaje(FluentValidation.Results.ValidationResult r) =>
        string.Join(" ", r.Errors.Select(e => e.ErrorMessage));

    private static string Mensaje(IdentityResult r) =>
        string.Join(" ", r.Errors.Select(e => e.Description));

    /// <summary>Genera una contraseña de 12 caracteres con mayúscula, minúscula, número y símbolo.</summary>
    private static string GenerarPasswordTemporal()
    {
        const string mayusculas = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string minusculas = "abcdefghijkmnopqrstuvwxyz";
        const string numeros = "23456789";
        const string simbolos = "!@#$%*?";
        const string todos = mayusculas + minusculas + numeros + simbolos;

        var caracteres = new List<char>
        {
            mayusculas[RandomNumberGenerator.GetInt32(mayusculas.Length)],
            minusculas[RandomNumberGenerator.GetInt32(minusculas.Length)],
            numeros[RandomNumberGenerator.GetInt32(numeros.Length)],
            simbolos[RandomNumberGenerator.GetInt32(simbolos.Length)]
        };

        while (caracteres.Count < 12)
            caracteres.Add(todos[RandomNumberGenerator.GetInt32(todos.Length)]);

        // Mezcla para que los 4 tipos no queden siempre al inicio.
        for (var i = caracteres.Count - 1; i > 0; i--)
        {
            var j = RandomNumberGenerator.GetInt32(i + 1);
            (caracteres[i], caracteres[j]) = (caracteres[j], caracteres[i]);
        }

        return new string(caracteres.ToArray());
    }
}