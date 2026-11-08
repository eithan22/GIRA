using System.Threading.RateLimiting;
using GIRA.Api.Common;
using Microsoft.AspNetCore.RateLimiting;

namespace GIRA.Api.Common.Reservas;

/// <summary>
/// Límites de intentos para los endpoints públicos de Reservas (RNF-06).
/// </summary>
public static class LimiteReservasExtensions
{
    /// <summary>Nombre de la política de consulta (disponibilidad) para <c>[EnableRateLimiting]</c>.</summary>
    public const string PoliticaConsulta = "reservas-consulta";

    /// <summary>Nombre de la política de creación (POST /api/reservas) para <c>[EnableRateLimiting]</c>.</summary>
    public const string PoliticaCreacion = "reservas-creacion";

    /// <summary>Nombre de la política de consulta por código (POST /api/reservas/consultar) para <c>[EnableRateLimiting]</c>.</summary>
    public const string PoliticaConsultaCodigo = "reservas-consulta-codigo";

    /// <summary>Nombre de la política de cancelación (PATCH /api/reservas/codigo/{codigo}/cancelar) para <c>[EnableRateLimiting]</c>.</summary>
    public const string PoliticaCancelacion = "reservas-cancelacion";

    /// <summary>
    /// Registra las políticas de consulta de disponibilidad (20/min), creación (5/min),
    /// consulta por código (10/min) y cancelación (5/min), cada una por IP.
    /// </summary>
    public static IServiceCollection AddLimiteReservas(this IServiceCollection services)
    {
        // Se configura solo la política, con su propio OnRejected (ver LimiteReservasConsultaPolicy
        // y LimiteReservasCreacionPolicy), para no depender de qué módulo registró el global de
        // LimiteLoginExtensions al final.
        services.AddRateLimiter(options =>
        {
            options.AddPolicy<string, LimiteReservasConsultaPolicy>(PoliticaConsulta);
            options.AddPolicy<string, LimiteReservasCreacionPolicy>(PoliticaCreacion);
            options.AddPolicy<string, LimiteReservasConsultaCodigoPolicy>(PoliticaConsultaCodigo);
            options.AddPolicy<string, LimiteReservasCancelacionPolicy>(PoliticaCancelacion);
        });

        return services;
    }
}

/// <summary>
/// Política de ventana fija por IP para <c>GET /api/reservas/disponibilidad</c>, con su propio
/// mensaje de rechazo (igual patrón que <c>LimiteOlvidePasswordPolicy</c> de Seguridad).
/// </summary>
public sealed class LimiteReservasConsultaPolicy : IRateLimiterPolicy<string>
{
    /// <summary>Responde 429 con un ProblemDetails y el mensaje neutro compartido.</summary>
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected =>
        (context, _) => new ValueTask(Results.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            detail: MensajesRateLimiting.Neutro)
            .ExecuteAsync(context.HttpContext));

    /// <summary>Agrupa por IP de origen; 20 solicitudes por minuto, sin cola.</summary>
    public RateLimitPartition<string> GetPartition(HttpContext httpContext) =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 20,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
}

/// <summary>
/// Política para <c>POST /api/reservas</c>: por usuario autenticado (30/min, el personal
/// puede registrar varias reservas seguidas) o por IP para invitados (5/min), más estricta
/// que la de consulta porque escribe en la base de datos. Requiere que UseAuthentication()
/// corra antes que UseRateLimiter() en Program.cs para que HttpContext.User esté poblado.
/// </summary>
public sealed class LimiteReservasCreacionPolicy : IRateLimiterPolicy<string>
{
    /// <summary>Responde 429 con un ProblemDetails y el mensaje neutro compartido.</summary>
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected =>
        (context, _) => new ValueTask(Results.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            detail: MensajesRateLimiting.Neutro)
            .ExecuteAsync(context.HttpContext));

    /// <summary>
    /// Si hay usuario autenticado, agrupa por su UsuarioId (30/min); si no, por IP de
    /// origen (5/min). Sin cola en ningún caso.
    /// </summary>
    public RateLimitPartition<string> GetPartition(HttpContext httpContext) =>
        httpContext.User.Identity?.IsAuthenticated == true
            ? RateLimitPartition.GetFixedWindowLimiter(
                $"usuario:{httpContext.User.ObtenerUsuarioId()}",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 30,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                })
            : RateLimitPartition.GetFixedWindowLimiter(
                httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = 5,
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0
                });
}

/// <summary>
/// Política de ventana fija por IP para <c>POST /api/reservas/consultar</c>. Más estricta
/// que la de disponibilidad porque el código de 8 caracteres es adivinable; en la práctica
/// este endpoint funciona como una autenticación por código+teléfono.
/// </summary>
public sealed class LimiteReservasConsultaCodigoPolicy : IRateLimiterPolicy<string>
{
    /// <summary>Responde 429 con un ProblemDetails y el mensaje neutro compartido.</summary>
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected =>
        (context, _) => new ValueTask(Results.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            detail: MensajesRateLimiting.Neutro)
            .ExecuteAsync(context.HttpContext));

    /// <summary>Agrupa por IP de origen; 10 solicitudes por minuto, sin cola.</summary>
    public RateLimitPartition<string> GetPartition(HttpContext httpContext) =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 10,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
}

/// <summary>
/// Política de ventana fija por IP para <c>PATCH /api/reservas/codigo/{codigo}/cancelar</c>,
/// igual de estricta que la de creación porque también escribe en la base de datos.
/// </summary>
public sealed class LimiteReservasCancelacionPolicy : IRateLimiterPolicy<string>
{
    /// <summary>Responde 429 con un ProblemDetails y el mensaje neutro compartido.</summary>
    public Func<OnRejectedContext, CancellationToken, ValueTask>? OnRejected =>
        (context, _) => new ValueTask(Results.Problem(
            statusCode: StatusCodes.Status429TooManyRequests,
            detail: MensajesRateLimiting.Neutro)
            .ExecuteAsync(context.HttpContext));

    /// <summary>Agrupa por IP de origen; 5 solicitudes por minuto, sin cola.</summary>
    public RateLimitPartition<string> GetPartition(HttpContext httpContext) =>
        RateLimitPartition.GetFixedWindowLimiter(
            httpContext.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            _ => new FixedWindowRateLimiterOptions
            {
                PermitLimit = 5,
                Window = TimeSpan.FromMinutes(1),
                QueueLimit = 0
            });
}
