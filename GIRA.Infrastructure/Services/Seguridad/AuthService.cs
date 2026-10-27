using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Seguridad.Auth;
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
    private readonly SignInManager<Usuario> _signInManager;
    private readonly ITokenService _tokenService;
    private readonly IValidator<LoginRequest> _validator;
    private readonly GiraDbContext _dbContext;
    private readonly IRegistroAccesoService _registroAcceso;
    private readonly IRotadorRefreshToken _rotadorRefreshToken;
    /// <summary>Crea el servicio con sus dependencias de Identity, tokens, validación y auditoría de accesos.</summary>
    public AuthService(
        UserManager<Usuario> userManager,
        SignInManager<Usuario> signInManager,
        ITokenService tokenService,
        IValidator<LoginRequest> validator,
        GiraDbContext dbContext,
        IRegistroAccesoService registroAcceso,
        IRotadorRefreshToken rotadorRefreshToken)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _validator = validator;
        _dbContext = dbContext;
        _registroAcceso = registroAcceso;
        _rotadorRefreshToken = rotadorRefreshToken;
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
            Usuario = MapeadorUsuarioAutenticado.ADto(usuario, roles)
        });
    }

    /// <summary>
    /// Renueva una sesión utilizando un refresh token.
    /// GI-119: Validación y rotación de tokens.
    /// Delega en <see cref="IRotadorRefreshToken"/> (Tanda C, 4.2).
    /// </summary>
    public Task<OperationResult<LoginResponse>> RefreshAsync(
        string refreshToken,
        string ip,
        CancellationToken cancellationToken = default) =>
        _rotadorRefreshToken.RotarAsync(refreshToken, ip, cancellationToken);



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
                MensajesSeguridad.TokenRenovacionInvalido);
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
                MensajesSeguridad.TokenRenovacionInvalido);
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
                MensajesSeguridad.TokenRenovacionInvalido);
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
                MensajesSeguridad.UsuarioNoAutorizado);
        }

        if (request.PasswordActual == request.PasswordNueva)
        {
            return OperationResult.Failure(
                ErrorType.Validation,
                "La contraseña nueva debe ser diferente de la actual.");
        }

        // Identity y los refresh tokens usan el mismo GiraDbContext.
        // Una transacción evita dejar la contraseña actualizada con tokens sin revocar.
        // Serializable es intencional y no debe bajarse sin analizar la carrera: evita que un
        // refresh concurrente cree una sesión nueva que sobreviva al cambio de contraseña.
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
