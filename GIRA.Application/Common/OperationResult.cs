namespace GIRA.Application.Common;

/// <summary>Resultado de un caso de uso, sin valor.</summary>
public class OperationResult
{
    /// <summary>Si tuvo éxito.</summary>
    public bool IsSuccess { get; }

    /// <summary>Si falló.</summary>
    public bool IsFailure => !IsSuccess;

    /// <summary>Tipo de error; null si tuvo éxito.</summary>
    public ErrorType? Type { get; }

    /// <summary>Mensaje de error; null si tuvo éxito.</summary>
    public string? Error { get; }

    /// <summary>
    /// Código estable del error (p. ej. "LIMITE_VIGENTES"), para que el cliente decida sin leer <see cref="Error"/>.
    /// Null si tuvo éxito o si el error todavía no tiene código asignado (sigue funcionando igual: ver
    /// <see cref="GIRA.Application.Common.Reservas.CodigosErrorReservas"/>).
    /// </summary>
    public string? Codigo { get; }

    /// <summary>Usar las fábricas estáticas, no este constructor.</summary>
    protected OperationResult(bool isSuccess, ErrorType? type, string? error, string? codigo = null)
    {
        IsSuccess = isSuccess;
        Type = type;
        Error = error;
        Codigo = codigo;
    }

    /// <summary>Resultado exitoso sin valor.</summary>
    public static OperationResult Success() => new(true, null, null);

    /// <summary>Resultado exitoso con valor.</summary>
    public static OperationResult<T> Success<T>(T value) => new(value);

    /// <summary>Resultado fallido sin valor, con código estable opcional.</summary>
    public static OperationResult Failure(ErrorType type, string error, string? codigo = null) => new(false, type, error, codigo);

    /// <summary>Resultado fallido con valor (Value queda en default), con código estable opcional.</summary>
    public static OperationResult<T> Failure<T>(ErrorType type, string error, string? codigo = null) => new(type, error, codigo);
}
