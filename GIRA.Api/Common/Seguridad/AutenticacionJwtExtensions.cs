using GIRA.Api.Common;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Services.Seguridad.Auth;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace GIRA.Api.Common.Seguridad;

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
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        var id = context.Principal?.ObtenerUsuarioId();
                        if (id is null)
                        {
                            context.Fail("Token inválido.");
                            return;
                        }

                        var db = context.HttpContext.RequestServices.GetRequiredService<GiraDbContext>();
                        if (!await db.Users.AsNoTracking().AnyAsync(u => u.Id == id.Value, context.HttpContext.RequestAborted))
                            context.Fail("La cuenta ya no existe.");
                    }
                };
            });

        return services;
    }
}
