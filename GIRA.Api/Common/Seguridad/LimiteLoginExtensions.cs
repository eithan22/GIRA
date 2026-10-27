using System.Threading.RateLimiting;

namespace GIRA.Api.Common.Seguridad;

/// <summary>Limita los intentos de inicio de sesión por IP contra la fuerza bruta (RNF-06).</summary>
public static class LimiteLoginExtensions
{
    /// <summary>Nombre de la política que se aplica con <c>[EnableRateLimiting]</c>.</summary>
    public const string Politica = "login";

    /// <summary>Registra la política: 10 intentos por minuto por IP; el siguiente recibe 429.</summary>
    public static IServiceCollection AddLimiteLogin(this IServiceCollection services)
    {
        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = (context, _) => new ValueTask(Results.Problem(
                statusCode: StatusCodes.Status429TooManyRequests,
                detail: "Demasiados intentos de inicio de sesión. Espera un minuto e inténtalo de nuevo.")
                .ExecuteAsync(context.HttpContext));

            // El límite es mayor que los 5 intentos del bloqueo de cuenta para no ocultarlo.
            options.AddPolicy(Politica, httpContext => RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 10,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                }));
        });

        return services;
    }
}
