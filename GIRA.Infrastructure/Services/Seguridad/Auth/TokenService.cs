using System.Buffers.Text;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using GIRA.Application.DTOs.Seguridad.Auth;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Domain.Entities.Seguridad;
using GIRA.Infrastructure.Persistence.Context;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace GIRA.Infrastructure.Services.Seguridad.Auth;

/// <summary>
/// Implementación de <see cref="ITokenService"/>: access tokens JWT firmados con
/// HMAC-SHA256 y refresh tokens aleatorios guardados como hash SHA-256.
/// </summary>
public class TokenService : ITokenService
{
    private readonly JwtSettings _jwt;
    private readonly GiraDbContext _dbContext;

    /// <summary>Crea el servicio con la configuración JWT y el contexto de datos.</summary>
    public TokenService(JwtSettings jwt, GiraDbContext dbContext)
    {
        _jwt = jwt;
        _dbContext = dbContext;
    }

    /// <inheritdoc />
    public TokenGenerado GenerarAccessToken(Guid usuarioId, string email, string nombreCompleto, IEnumerable<string> roles)
    {
        var ahora = DateTime.UtcNow;
        var expiraEn = ahora.AddMinutes(_jwt.AccessTokenMinutos);

        var claims = new List<Claim>
        {
            new(TiposClaim.UsuarioId, usuarioId.ToString()),
            new(TiposClaim.Email, email),
            new(TiposClaim.Nombre, nombreCompleto),
            new(TiposClaim.TokenId, Guid.NewGuid().ToString())
        };
        claims.AddRange(roles.Select(rol => new Claim(TiposClaim.Rol, rol)));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            IssuedAt = ahora,
            NotBefore = ahora,
            Expires = expiraEn,
            SigningCredentials = new SigningCredentials(_jwt.ClaveFirma(), SecurityAlgorithms.HmacSha256)
        };

        return new TokenGenerado(new JsonWebTokenHandler().CreateToken(descriptor), expiraEn);
    }

    /// <inheritdoc />
    public async Task<TokenGenerado> CrearRefreshTokenAsync(Guid usuarioId, string ip, CancellationToken cancellationToken = default)
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(64));
        var ahora = DateTime.UtcNow;

        var refreshToken = new RefreshToken
        {
            UsuarioId = usuarioId,
            TokenHash = CalcularHash(token),
            FechaCreacion = ahora,
            FechaExpiracion = ahora.AddDays(_jwt.RefreshTokenDias),
            IpCreacion = ip
        };

        _dbContext.RefreshTokens.Add(refreshToken);
        await _dbContext.SaveChangesAsync(cancellationToken);

        return new TokenGenerado(token, refreshToken.FechaExpiracion);
    }

    /// <inheritdoc />
    public string CalcularHash(string token)
    {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
