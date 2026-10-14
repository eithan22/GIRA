using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Seguridad;
using GIRA.Application.Interfaces.Services.Seguridad;
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
    private readonly SignInManager<Usuario> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly IValidator<LoginRequest> _validator;
    private readonly GiraDbContext _dbContext;
    /// <summary>Crea el servicio con sus dependencias de Identity, tokens y validación.</summary>
    public AuthService(
    UserManager<Usuario> userManager,
    SignInManager<Usuario> signInManager,
    ITokenService tokenService,
    IValidator<LoginRequest> validator,
    GiraDbContext dbContext)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _validator = validator;
        _dbContext = dbContext;
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

        // Pendiente GI-102: registrar cada intento (exitoso o no, con su motivo) en
        // RegistroAcceso cuando Ismael publique la interfaz RegistrarAsync.

        var usuario = await _userManager.FindByEmailAsync(request.Email);
        if (usuario is null || !usuario.Activo)
        {
            return CredencialesInvalidas();
        }

        // Con lockoutOnFailure, Identity suma cada fallo y bloquea la cuenta al llegar al
        // máximo configurado (5 intentos, 15 minutos, RN-SEG-03). Mientras dura el bloqueo
        // el resultado es fallido aunque la contraseña sea correcta.
        var resultado = await _signInManager.CheckPasswordSignInAsync(usuario, request.Password, lockoutOnFailure: true);
        if (!resultado.Succeeded)
        {
            return CredencialesInvalidas();
        }

        // Los roles se leen de la base en cada emisión: un cambio de roles se refleja en el siguiente token.
        var roles = await _userManager.GetRolesAsync(usuario);
        var accessToken = _tokenService.GenerarAccessToken(usuario.Id, usuario.Email!, usuario.NombreCompleto, roles);
        var refreshToken = await _tokenService.CrearRefreshTokenAsync(usuario.Id, ip, cancellationToken);

        // UltimoAcceso es informativo: si la actualización falla, el inicio de sesión sigue siendo válido.
        usuario.UltimoAcceso = DateTime.UtcNow;
        await _userManager.UpdateAsync(usuario);



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
                EmpleadoId = usuario.EmpleadoId
            }
        });
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
            .AsNoTracking()
            .FirstOrDefaultAsync(
                t => t.TokenHash == hash,
                cancellationToken);

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
                EmpleadoId = usuario.EmpleadoId
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
        if (tokenGuardado.FechaRevocacion is not null ||
            tokenGuardado.FechaExpiracion <= DateTime.UtcNow)
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


    private static OperationResult<LoginResponse> CredencialesInvalidas()
    {
        return OperationResult.Failure<LoginResponse>(ErrorType.Unauthorized, MensajeCredencialesInvalidas);
    }
}
