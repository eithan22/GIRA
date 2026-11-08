namespace GIRA.Domain.Exceptions.Reservas;

/// <summary>
/// Se lanza cuando se intenta guardar una reserva que fue modificada por otra operación
/// entre la lectura y el guardado (choque de RowVersion, DbUpdateConcurrencyException).
/// </summary>
public class ReservaConflictoConcurrenciaException : Exception
{
    /// <summary>Crea la excepción con la causa original de la base de datos.</summary>
    public ReservaConflictoConcurrenciaException(string mensaje, Exception innerException) : base(mensaje, innerException)
    {
    }
}
