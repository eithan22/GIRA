using System.Threading.RateLimiting;
using GIRA.Api.Common;
using Microsoft.AspNetCore.RateLimiting;

namespace GIRA.Api.Common.Reservas;

/// <summary>
/// Límites de intentos para los endpoints públicos de Reservas (RNF-06). Las políticas de
/// consultar por código y cancelar se agregan después en este mismo archivo.
/// </summary>
public static class LimiteReservasExtensions
{
    /// <summary>Nombre de la política de consulta (disponibilidad) para <c>[EnableRateLimiting]</c>.</summary>
    public const string PoliticaConsulta = "reservas-consulta";

    /// <summary>Nombre de la política de creación (POST /api/reservas) para <c>[EnableRateLimiting]</c>.</summary>
    public const string PoliticaCreacion = "reservas-creacion";

    /// <summary>Registra las políticas de consulta (20/min) y creación (5/min), cada una por IP.</summary>
    public static IServiceCollection AddLimiteReservas(this IServiceCollection services)
    {
        // Se configura solo la política, con su propio OnRejected (ver LimiteReservasConsultaPolicy
        // y LimiteReservasCreacionPolicy), para no depender de qué módulo registró el global de
        // LimiteLoginExtensions al final.
        services.AddRateLimiter(options =>
        {
            options.AddPolicy<string, LimiteReservasConsultaPolicy>(PoliticaConsulta);
            options.AddPolicy<string, LimiteReservasCreacionPolicy>(PoliticaCreacion);
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
/// Política de ventana fija por IP para <c>POST /api/reservas</c>, más estricta que la de
/// consulta porque escribe en la base de datos.
/// </summary>
public sealed class LimiteReservasCreacionPolicy : IRateLimiterPolicy<string>
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
