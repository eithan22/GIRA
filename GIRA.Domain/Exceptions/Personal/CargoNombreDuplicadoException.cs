namespace GIRA.Domain.Exceptions.Personal;

/// <summary>
/// Se intentó guardar un cargo con un nombre que otra solicitud acababa de registrar
/// (choque con el índice único de nombre).
/// </summary>
public class CargoNombreDuplicadoException : Exception
{
    /// <summary>Crea la excepción con la causa original de la base de datos.</summary>
    public CargoNombreDuplicadoException(string mensaje, Exception innerException) : base(mensaje, innerException)
    {
    }
}
