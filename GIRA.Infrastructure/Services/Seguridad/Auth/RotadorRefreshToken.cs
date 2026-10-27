using GIRA.Application.Common;
using GIRA.Application.DTOs.Seguridad.Auth;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Infrastructure.Identity;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Data;

namespace GIRA.Infrastructure.Services.Seguridad;

/// <summary>
/// Implementación de <see cref="IRotadorRefreshToken"/>. Extraído de
/// AuthService.RefreshAsync (Tanda C, 4.2) sin cambiar su lógica.
/// </summary>
public class RotadorRefreshToken : IRotadorRefreshToken
{
    private readonly UserManager<Usuario> _userManager;
    private readonly ITokenService _tokenService;
    private readonly GiraDbContext _dbContext;

    /// <summary>Crea el rotador con sus dependencias de Identity, tokens y persistencia.</summary>
    public RotadorRefreshToken(
        UserManager<Usuario> userManager,
        ITokenService tokenService,
        GiraDbContext dbContext)
    {
        _userManager = userManager;
        _tokenService = tokenService;
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public async Task<OperationResult<LoginResponse>> RotarAsync(
        string refreshToken,
        string ip,
        CancellationToken cancellationToken = default)
    {
        OperationResult<LoginResponse> NoAutorizado() =>
            OperationResult.Failure<LoginResponse>(
                ErrorType.Unauthorized, MensajesSeguridad.TokenRenovacionInvalido);

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
            Usuario = MapeadorUsuarioAutenticado.ADto(usuario, roles)
        });
    }
}
