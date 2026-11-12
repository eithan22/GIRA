namespace GIRA.Application.Common;

/// <summary>Resultado de un caso de uso, con valor.</summary>
public class OperationResult<T> : OperationResult
{
    /// <summary>Valor devuelto; null si falló.</summary>
    public T? Value { get; }

    /// <summary>Resultado exitoso con valor.</summary>
    internal OperationResult(T value) : base(true, null, null)
    {
        Value = value;
    }

    /// <summary>Resultado fallido con valor.</summary>
    internal OperationResult(ErrorType type, string error) : base(false, type, error)
    {
        Value = default;
    }
}
