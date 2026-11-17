namespace GIRA.Domain.Exceptions.Reservas;

/// <summary>
/// Se lanza cuando se pide una transición de estado que la reserva no permite desde su
/// estado actual (por ejemplo, cancelar una reserva que ya está Completada).
/// </summary>
public class TransicionReservaInvalidaException : Exception
{
    /// <summary>Crea la excepción con el mensaje que explica la transición rechazada.</summary>
    public TransicionReservaInvalidaException(string mensaje) : base(mensaje)
    {
    }
}
