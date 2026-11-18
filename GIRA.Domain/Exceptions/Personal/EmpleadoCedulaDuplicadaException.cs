namespace GIRA.Domain.Exceptions.Personal;

/// <summary>
/// Se intentó guardar un empleado con una cédula que otra solicitud acababa de registrar
/// (choque con el índice único de cédula).
/// </summary>
public class EmpleadoCedulaDuplicadaException : Exception
{
    /// <summary>Crea la excepción con la causa original de la base de datos.</summary>
    public EmpleadoCedulaDuplicadaException(string mensaje, Exception innerException) : base(mensaje, innerException)
    {
    }
}
