using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Seguridad;
using GIRA.Application.Interfaces.Services.Seguridad;
using GIRA.Domain.Constants;
using GIRA.Infrastructure.Identity;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace GIRA.Infrastructure.Services.Seguridad;

/// <summary>
/// Implementación de <see cref="IAuthService"/> con ASP.NET Core Identity. Vive en
/// Infrastructure porque necesita <see cref="UserManager{TUser}"/> y la entidad <see cref="Usuario"/>.
/// </summary>
public class AuthService : IAuthService
{
    /// <summary>
    /// Mensaje único para todo fallo de credenciales: no revela si el correo existe ni si
    /// la cuenta está bloqueada o desactivada (RF-SEG-01).
    /// </summary>
    public const string MensajeCredencialesInvalidas = "Correo o contraseña incorrectos.";

    private readonly UserManager<Usuario> _userManager;
    private readonly RoleManager<Rol> _roleManager;
    private readonly SignInManager<Usuario> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly IValidator<LoginRequest> _validator;
    private readonly IValidator<RegistroUsuarioRequest> _registroValidator;
    private readonly GiraDbContext _dbContext;
    private readonly IRegistroAccesoService _registroAcceso;
    /// <summary>Crea el servicio con sus dependencias de Identity, tokens, validación y auditoría de accesos.</summary>
    public AuthService(
        UserManager<Usuario> userManager,
        RoleManager<Rol> roleManager,
        SignInManager<Usuario> signInManager,
        ITokenService tokenService,
        IValidator<LoginRequest> validator,
        IValidator<RegistroUsuarioRequest> registroValidator,
        GiraDbContext dbContext,
        IRegistroAccesoService registroAcceso)
    {
        _userManager = userManager;
        _roleManager = roleManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _validator = validator;
        _registroValidator = registroValidator;
        _dbContext = dbContext;
        _registroAcceso = registroAcceso;
    }

    /// <inheritdoc />
    public async Task<OperationResult<LoginResponse>> LoginAsync(LoginRequest request, string ip, CancellationToken cancellationToken = default)
    {
        var validacion = await _validator.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure<LoginResponse>(
                ErrorType.Validation, string.Join(" ", validacion.Errors.Select(e => e.ErrorMessage)));
        }

        var usuario = await _userManager.FindByEmailAsync(request.Email);
        if (usuario is null)
        {
            await _registroAcceso.RegistrarAsync(null, request.Email, ip, false, MotivosAcceso.UsuarioNoExiste, cancellationToken);
            return CredencialesInvalidas();
        }

        if (!usuario.Activo)
        {
            await _registroAcceso.RegistrarAsync(usuario.Id, request.Email, ip, false, MotivosAcceso.Inactivo, cancellationToken);
            return CredencialesInvalidas();
        }

        // Con lockoutOnFailure, Identity suma cada fallo y bloquea la cuenta al llegar al
        // máximo configurado (5 intentos, 15 minutos, RN-SEG-03). Mientras dura el bloqueo
        // el resultado es fallido aunque la contraseña sea correcta.
        var resultado = await _signInManager.CheckPasswordSignInAsync(usuario, request.Password, lockoutOnFailure: true);
        if (!resultado.Succeeded)
        {
            var motivo = resultado.IsLockedOut ? MotivosAcceso.Bloqueado : MotivosAcceso.PasswordIncorrecta;
            await _registroAcceso.RegistrarAsync(usuario.Id, request.Email, ip, false, motivo, cancellationToken);
            return CredencialesInvalidas();
        }

        // Los roles se leen de la base en cada emisión: un cambio de roles se refleja en el siguiente token.
        var roles = await _userManager.GetRolesAsync(usuario);
        var accessToken = _tokenService.GenerarAccessToken(usuario.Id, usuario.Email!, usuario.NombreCompleto, roles);
        var refreshToken = await _tokenService.CrearRefreshTokenAsync(usuario.Id, ip, cancellationToken);

        // UltimoAcceso es informativo: si la actualización falla, el inicio de sesión sigue siendo válido.
        usuario.UltimoAcceso = DateTime.UtcNow;
        await _userManager.UpdateAsync(usuario);

        await _registroAcceso.RegistrarAsync(usuario.Id, request.Email, ip, true, null, cancellationToken);

        return OperationResult.Success(new LoginResponse
        {
            AccessToken = accessToken.Token,
            AccessTokenExpiraEn = accessToken.ExpiraEn,
            RefreshToken = refreshToken.Token,
            RefreshTokenExpiraEn = refreshToken.ExpiraEn,
            Usuario = new UsuarioAutenticadoDto
            {
                Id = usuario.Id,
                Email = usuario.Email!,
                NombreCompleto = usuario.NombreCompleto,
                Roles = roles.ToList(),
                EmpleadoId = usuario.EmpleadoId,
                DebeCambiarPassword = usuario.DebeCambiarPassword
            }
        });
    }

    public async Task<OperationResult> RegistrarAsync(
        RegistroUsuarioRequest request,
        CancellationToken cancellationToken = default)
    {
        var validacion = await _registroValidator.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            return OperationResult.Failure(
                ErrorType.Validation,
                string.Join(" ", validacion.Errors.Select(error => error.ErrorMessage)));
        }

        var email = request.Email.Trim();
        if (await _userManager.FindByEmailAsync(email) is not null)
        {
            return OperationResult.Failure(
                ErrorType.Conflict,
                "Ya existe una cuenta con ese correo. Inicia sesión o utiliza otro correo.");
        }

        if (!await _roleManager.RoleExistsAsync(Roles.Mesero))
        {
            throw new InvalidOperationException(
                $"El rol fijo de registro '{Roles.Mesero}' no está configurado.");
        }

        var usuario = new Usuario
        {
            Id = Guid.NewGuid(),
            UserName = email,
            Email = email,
            NombreCompleto = request.NombreCompleto.Trim(),
            Activo = true,
            DebeCambiarPassword = false,
            FechaCreacion = DateTime.UtcNow
        };

        var creado = await _userManager.CreateAsync(usuario, request.Password);
        if (!creado.Succeeded)
        {
            if (creado.Errors.Any(error =>
                    error.Code is "DuplicateEmail" or "DuplicateUserName"))
            {
                return OperationResult.Failure(
                    ErrorType.Conflict,
                    "Ya existe una cuenta con ese correo. Inicia sesión o utiliza otro correo.");
            }

            return OperationResult.Failure(
                ErrorType.Validation,
                string.Join(" ", creado.Errors.Select(error => error.Description)));
        }

        var asignacionRol = await _userManager.AddToRoleAsync(usuario, Roles.Mesero);
        if (!asignacionRol.Succeeded)
        {
            var eliminacion = await _userManager.DeleteAsync(usuario);
            if (!eliminacion.Succeeded)
            {
                throw new InvalidOperationException(
                    $"No se pudo asignar el rol de registro ni limpiar la cuenta creada. " +
                    $"Asignación: {string.Join(" ", asignacionRol.Errors.Select(error => error.Description))} " +
                    $"Limpieza: {string.Join(" ", eliminacion.Errors.Select(error => error.Description))}");
            }

            throw new InvalidOperationException(
                $"No se pudo asignar el rol fijo de registro '{Roles.Mesero}': " +
                string.Join(" ", asignacionRol.Errors.Select(error => error.Description)));
        }

        return OperationResult.Success();
    }


    /// <summary>
    /// Renueva una sesión utilizando un refresh token.
    /// GI-119: Validación y rotación de tokens.
    /// </summary>

    public async Task<OperationResult<LoginResponse>> RefreshAsync(
        string refreshToken,
        string ip,
        CancellationToken cancellationToken = default)
    {
        const string mensajeError = "Token de renovación inválido.";

        OperationResult<LoginResponse> NoAutorizado() =>
            OperationResult.Failure<LoginResponse>(
                ErrorType.Unauthorized, mensajeError);

        if (string.IsNullOrWhiteSpace(refreshToken))
            return NoAutorizado();

        var hash = _tokenService.CalcularHash(refreshToken);

        await using var transaccion =
            await _dbContext.Database.BeginTransactionAsync(
                IsolationLevel.Serializable,
                cancellationToken);


        var tokenAnterior = await _dbContext.RefreshTokens
            .FromSqlInterpolated($@"
        SELECT *
        FROM [seguridad].[RefreshToken]
             WITH (UPDLOCK, HOLDLOCK)
        WHERE [TokenHash] = {hash}")
            .AsNoTracking()
            .FirstOrDefaultAsync(cancellationToken);


        if (tokenAnterior is null)
            return NoAutorizado();

        // Detectar reutilización de un token reemplazado.
        if (tokenAnterior.ReemplazadoPorId is not null)
        {
            var ahora = DateTime.UtcNow;

            await _dbContext.RefreshTokens
                .Where(t =>
                    t.UsuarioId == tokenAnterior.UsuarioId &&
                    t.FechaRevocacion == null)
                .ExecuteUpdateAsync(
                    setters => setters.SetProperty(
                        t => t.FechaRevocacion,
                        ahora),
                    cancellationToken);

            await transaccion.CommitAsync(cancellationToken);
            return NoAutorizado();
        }

        if (tokenAnterior.FechaRevocacion is not null ||
            tokenAnterior.FechaExpiracion <= DateTime.UtcNow)
        {
            return NoAutorizado();
        }

        // Consultar el usuario y sus roles actuales.
        var usuario = await _userManager.FindByIdAsync(
            tokenAnterior.UsuarioId.ToString());

        if (usuario is null || !usuario.Activo ||
            string.IsNullOrWhiteSpace(usuario.Email))
        {
            return NoAutorizado();
        }

        var roles = await _userManager.GetRolesAsync(usuario);

        // Consumir el token una sola vez.
        var ahoraRotacion = DateTime.UtcNow;

        var filasActualizadas = await _dbContext.RefreshTokens
            .Where(t =>
                t.Id == tokenAnterior.Id &&
                t.FechaRevocacion == null &&
                t.ReemplazadoPorId == null &&
                t.FechaExpiracion > ahoraRotacion)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    t => t.FechaRevocacion,
                    ahoraRotacion),
                cancellationToken);

        if (filasActualizadas != 1)
            return NoAutorizado();

        // Reutilizar la generación de Luis.
        var nuevoRefresh = await _tokenService
            .CrearRefreshTokenAsync(
                usuario.Id, ip, cancellationToken);

        // Recuperar el identificador del token recién creado.
        var nuevoHash = _tokenService.CalcularHash(
            nuevoRefresh.Token);

        var nuevoRegistro = await _dbContext.RefreshTokens
            .SingleAsync(
                t => t.TokenHash == nuevoHash,
                cancellationToken);

        // Enlazar el token anterior con su reemplazo.
        await _dbContext.RefreshTokens
            .Where(t => t.Id == tokenAnterior.Id)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(
                    t => t.ReemplazadoPorId,
                    nuevoRegistro.Id),
                cancellationToken);

        var nuevoAccess = _tokenService.GenerarAccessToken(
            usuario.Id,
            usuario.Email!,
            usuario.NombreCompleto,
            roles);

        await transaccion.CommitAsync(cancellationToken);

        return OperationResult.Success(new LoginResponse
        {
            AccessToken = nuevoAccess.Token,
            AccessTokenExpiraEn = nuevoAccess.ExpiraEn,
            RefreshToken = nuevoRefresh.Token,
            RefreshTokenExpiraEn = nuevoRefresh.ExpiraEn,
            Usuario = new UsuarioAutenticadoDto
            {
                Id = usuario.Id,
                Email = usuario.Email!,
                NombreCompleto = usuario.NombreCompleto,
                Roles = roles.ToList(),
                EmpleadoId = usuario.EmpleadoId,
                DebeCambiarPassword = usuario.DebeCambiarPassword
            }
        });
    }



    /// <summary>
    /// GI-96: Cierra la sesión revocando el refresh token
    /// correspondiente al usuario autenticado.
    /// </summary>
    public async Task<OperationResult> LogoutAsync(
        Guid usuarioId,
        string refreshToken,
        CancellationToken cancellationToken = default)
    {
        // 1. Verificar que se recibió un token.
        if (string.IsNullOrWhiteSpace(refreshToken))
        {
            return OperationResult.Failure(
                ErrorType.Unauthorized,
                "Token de renovación inválido.");
        }

        // 2. Calcular el hash mediante el servicio existente.
        var hash = _tokenService.CalcularHash(refreshToken);

        // 3. Buscar el token y comprobar su propietario.
        var tokenGuardado = await _dbContext.RefreshTokens
            .FirstOrDefaultAsync(
                t => t.TokenHash == hash &&
                     t.UsuarioId == usuarioId,
                cancellationToken);

        // 4. Rechazar tokens inexistentes o de otro usuario.
        if (tokenGuardado is null)
        {
            return OperationResult.Failure(
                ErrorType.Unauthorized,
                "Token de renovación inválido.");
        }

        // 5. Comprobar si el token ya fue revocado o venció.
        if (tokenGuardado.FechaRevocacion is not null)
        {
            return OperationResult.Success();
        }

        if (tokenGuardado.FechaExpiracion <= DateTime.UtcNow)
        {
            return OperationResult.Failure(
                ErrorType.Unauthorized,
                "Token de renovación inválido.");
        }

        // 6. Revocar el token actual.
        tokenGuardado.FechaRevocacion = DateTime.UtcNow;

        // 7. Guardar el cambio en SQL Server.
        await _dbContext.SaveChangesAsync(cancellationToken);

        // 8. Confirmar el cierre de sesión.
        return OperationResult.Success();
    }


    /// <summary>
    /// GI-143: Cambia la contraseña del usuario autenticado y revoca sus refresh tokens.
    /// </summary>
    public async Task<OperationResult> CambiarPasswordAsync(
        Guid usuarioId,
        CambiarPasswordRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request is null ||
            string.IsNullOrWhiteSpace(request.PasswordActual) ||
            string.IsNullOrWhiteSpace(request.PasswordNueva))
        {
            return OperationResult.Failure(
                ErrorType.Validation,
                "Debes proporcionar la contraseña actual y la nueva.");
        }

        var usuario = await _userManager.FindByIdAsync(usuarioId.ToString());
        if (usuario is null || !usuario.Activo)
        {
            return OperationResult.Failure(
                ErrorType.Unauthorized,
                "Usuario no autorizado.");
        }

        if (!await _userManager.CheckPasswordAsync(usuario, request.PasswordActual))
        {
            return OperationResult.Failure(
                ErrorType.Validation,
                "La contraseña actual es incorrecta.");
        }

        if (request.PasswordActual == request.PasswordNueva ||
            await _userManager.CheckPasswordAsync(usuario, request.PasswordNueva))
        {
            return OperationResult.Failure(
                ErrorType.Validation,
                "La contraseña nueva debe ser diferente de la actual.");
        }

        var nueva = request.PasswordNueva;
        if (nueva.Length < 8 ||
            !nueva.Any(char.IsUpper) ||
            !nueva.Any(char.IsLower) ||
            !nueva.Any(char.IsDigit) ||
            !nueva.Any(c => !char.IsLetterOrDigit(c)))
        {
            return OperationResult.Failure(
                ErrorType.Validation,
                "La nueva contraseña debe tener al menos 8 caracteres, una mayúscula, " +
                "una minúscula, un número y un símbolo.");
        }

        // Identity y los refresh tokens usan el mismo GiraDbContext.
        // Una transacción evita dejar la contraseña actualizada con tokens sin revocar.
        await using var transaccion = await _dbContext.Database.BeginTransactionAsync(
            IsolationLevel.Serializable,
            cancellationToken);

        // Identity guarda la contraseña y los campos modificados del usuario
        // mediante una única actualización. No necesitamos otro UpdateAsync.
        usuario.DebeCambiarPassword = false;
        var cambio = await _userManager.ChangePasswordAsync(
            usuario,
            request.PasswordActual,
            request.PasswordNueva);

        if (!cambio.Succeeded)
        {
            return OperationResult.Failure(
                ErrorType.Validation,
                string.Join(" ", cambio.Errors.Select(e => e.Description)));
        }

        // El endpoint no recibe un identificador del refresh token actual.
        // Por lo tanto, se revocan TODOS los tokens activos de este usuario.
        var ahora = DateTime.UtcNow;
        await _dbContext.RefreshTokens
            .Where(t => t.UsuarioId == usuarioId &&
                        t.FechaRevocacion == null)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(t => t.FechaRevocacion, ahora),
                cancellationToken);

        await transaccion.CommitAsync(cancellationToken);
        return OperationResult.Success();
    }

    private static OperationResult<LoginResponse> CredencialesInvalidas()
    {
        return OperationResult.Failure<LoginResponse>(ErrorType.Unauthorized, MensajeCredencialesInvalidas);
    }
}
