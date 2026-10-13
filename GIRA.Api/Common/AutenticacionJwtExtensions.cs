using GIRA.Infrastructure.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace GIRA.Api.Common;

/// <summary>Configura la autenticación con JWT Bearer (GI-95).</summary>
public static class AutenticacionJwtExtensions
{
    /// <summary>Valida firma, emisor, audiencia y vencimiento de los access tokens que emite la API.</summary>
    public static IServiceCollection AddAutenticacionJwt(this IServiceCollection services, IConfiguration configuration)
    {
        var jwt = JwtSettings.Leer(configuration);

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Conserva los nombres cortos de los claims ("sub", "role"); ver TiposClaim.
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = jwt.ClaveFirma(),
                    ValidateLifetime = true,
                    // Sin margen extra: el access token vence exactamente a los 30 minutos.
                    ClockSkew = TimeSpan.Zero,
                    NameClaimType = TiposClaim.Nombre,
                    RoleClaimType = TiposClaim.Rol
                };
            });

        return services;
    }
}
