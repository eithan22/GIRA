using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Common;

/// <summary>
/// Captura cualquier excepción no controlada que llegue hasta la API y responde con un
/// <see cref="ProblemDetails"/> 500 genérico, sin exponer el mensaje ni el stack trace real.
/// </summary>
public class ManejadorExcepcionesGlobal : IExceptionHandler
{
    private readonly ILogger<ManejadorExcepcionesGlobal> _logger;

    /// <summary>Crea el manejador con el logger donde quedará registrada la excepción real.</summary>
    public ManejadorExcepcionesGlobal(ILogger<ManejadorExcepcionesGlobal> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        // El cliente canceló la petición (por ejemplo, cerró la pestaña); no es un error del
        // servidor y no hay nada que responder. Se deja que siga el flujo normal del framework.
        if (exception is OperationCanceledException)
        {
            return false;
        }

        _logger.LogError(exception, "Excepción no controlada en {Ruta}", httpContext.Request.Path);

        httpContext.Response.StatusCode = StatusCodes.Status500InternalServerError;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Error interno",
                Detail = "Ocurrió un error inesperado. Inténtalo de nuevo más tarde."
            },
            options: null,
            contentType: "application/problem+json",
            cancellationToken: cancellationToken);

        return true;
    }
}
