namespace GIRA.Domain.Exceptions.Reservas;

/// <summary>
/// Se lanza cuando se intenta guardar una reserva para una mesa, fecha y franja que ya tiene
/// otra reserva vigente (choque de doble reserva, protegido por el índice único filtrado).
/// </summary>
public class ReservaDuplicadaException : Exception
{
    /// <summary>Crea la excepción con la causa original de la base de datos.</summary>
    public ReservaDuplicadaException(string mensaje, Exception innerException) : base(mensaje, innerException)
    {
    }
}
