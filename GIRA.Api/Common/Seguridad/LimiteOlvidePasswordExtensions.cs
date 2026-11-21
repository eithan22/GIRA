using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace GIRA.Api.Common.Seguridad;

/// <summary>
/// Limita las solicitudes de recuperación de contraseña por IP (GI-212, RNF-06).
/// Es más estricta que la del login porque el endpoint es anónimo y escribe en la base:
/// sin límite, cualquiera podría llenar la cola del Administrador con solicitudes.
/// </summary>
public static class LimiteOlvidePasswordExtensions
{
    /// <summary>Nombre de la política que se aplica con <c>[EnableRateLimiting]</c>.</summary>
    public const string Politica = "olvide-password";

    /// <summary>Mensaje del 429; no menciona el correo para no revelar si existe.</summary>
    public const string MensajeRechazo =
        "Demasiadas solicitudes de recuperación. Espera unos minutos e inténtalo de nuevo.";

    /// <summary>Registra la política: 3 solicitudes cada 10 minutos por IP; la siguiente recibe 429.</summary>
    public static IServiceCollection AddLimiteOlvidePassword(this IServiceCollection services)
    {
        // Se configura solo la política, sin tocar OnRejected global: el mensaje propio lo
        // aporta la política (ver LimiteOlvidePasswordPolicy), para no sobrescribir el del login.
        services.AddRateLimiter(options =>
            options.AddPolicy<string, LimiteOlvidePasswordPolicy>(Politica));

        return services;
    }
}

/// <summary>
/// Política de ventana fija por IP para <c>POST /api/auth/olvide-password</c>, con su propio
/// mensaje de rechazo. Se implementa como <see cref="IRateLimiterPolicy{TPartitionKey}"/> en vez
/// de con una lambda porque la sobrecarga con lambda no permite un <c>OnRejected</c> propio y
/// habría que cambiar el global, compartido con la política del login.
/// </summary>
public sealed class LimiteOlvidePasswordPolicy : IRateLimiterPolicy<string>
{
    /// <summary>Responde 429 con un ProblemDetails y el mensaje propio de esta política.</summary>
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected =>
        (context, _) => new ValueTask(Results.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            detail: LimiteOlvidePasswordExtensions.MensajeRechazo)
            .ExecuteAsync(context.HttpContext));

    /// <summary>Agrupa por IP de origen; 3 solicitudes cada 10 minutos, sin cola.</summary>
    public RateLimitPartition<string> GetPartition(HttpContext httpContext) =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 3,
                Window = TimeSpan.FromMinutes(10),
                QueueLimit = 0
            });
}
