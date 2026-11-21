namespace GIRA.Application.Common;

/// <summary>Tipo de error para mapear a código HTTP.</summary>
public enum ErrorType
{
    /// <summary>Datos de entrada inválidos (400).</summary>
    Validation,
    /// <summary>El recurso no existe (404).</summary>
    NotFound,
    /// <summary>El estado actual impide la operación (409).</summary>
    Conflict,
    /// <summary>Sin permiso para la operación (401).</summary>
    Unauthorized
}
