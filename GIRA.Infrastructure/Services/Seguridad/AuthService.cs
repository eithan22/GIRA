using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Seguridad;
using GIRA.Application.Interfaces.Services.Seguridad;
using GIRA.Domain.Constants;
using GIRA.Infrastructure.Identity;
using Microsoft.AspNetCore.Identity;

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
    private readonly IRegistroAccesoService _registroAcceso;

    /// <summary>Crea el servicio con sus dependencias de Identity, tokens, validación y auditoría de accesos.</summary>
    public AuthService(
        UserManager<Usuario> userManager,
        SignInManager<Usuario> signInManager,
        ITokenService tokenService,
        IValidator<LoginRequest> validator,
        IRegistroAccesoService registroAcceso)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _tokenService = tokenService;
        _validator = validator;
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
                EmpleadoId = usuario.EmpleadoId
            }
        });
    }

    private static OperationResult<LoginResponse> CredencialesInvalidas()
    {
        return OperationResult.Failure<LoginResponse>(ErrorType.Unauthorized, MensajeCredencialesInvalidas);
    }
}