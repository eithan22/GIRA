namespace GIRA.Domain.Exceptions.Reservas;

/// <summary>
/// Se lanza cuando dos solicitudes crean, al mismo tiempo, un <see cref="Entities.Reservas.Cliente"/>
/// con el mismo teléfono. No es un error para el usuario final: quien la atrapa debe volver a
/// buscar el cliente por teléfono y usar el que ya quedó guardado.
/// </summary>
public class ClienteDuplicadoException : Exception
{
    /// <summary>Crea la excepción con la causa original de la base de datos.</summary>
    public ClienteDuplicadoException(string mensaje, Exception innerException) : base(mensaje, innerException)
    {
    }
}
