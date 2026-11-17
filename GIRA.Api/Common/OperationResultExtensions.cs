using GIRA.Application.Common;
using Microsoft.AspNetCore.Mvc;

namespace GIRA.Api.Common;

/// <summary>Convierte OperationResult en IActionResult.</summary>
public static class OperationResultExtensions
{
    /// <summary>Éxito -> 204; fallo -> ProblemDetails.</summary>
    public static IActionResult ToActionResult(this OperationResult result)
    {
        return result.IsSuccess
            ? new NoContentResult()
            : Problema(result.Type!.Value, result.Error!, result.Codigo);
    }

    /// <summary>Éxito con valor -> 200; fallo -> ProblemDetails.</summary>
    public static IActionResult ToActionResult<T>(this OperationResult<T> result)
    {
        return result.IsSuccess
            ? new OkObjectResult(result.Value)
            : Problema(result.Type!.Value, result.Error!, result.Codigo);
    }

    // Mapea ErrorType a código HTTP. Si el OperationResult trae un código estable (ver
    // CodigosErrorReservas), se agrega como extensión "codigo" del ProblemDetails, de forma
    // compatible: los errores sin código (la mayoría, hoy) siguen devolviendo el mismo cuerpo que antes.
    private static ObjectResult Problema(ErrorType type, string detalle, string? codigo = null)
    {
        var statusCode = type switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.Unauthorized => StatusCodes.Status401Unauthorized,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

        var problema = new ProblemDetails { Status = statusCode, Detail = detalle };
        if (codigo is not null)
        {
            problema.Extensions["codigo"] = codigo;
        }

        return new ObjectResult(problema)
        {
            StatusCode = statusCode
        };
    }
}
