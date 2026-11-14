namespace GIRA.Domain.Exceptions.Configuracion;

/// <summary>
/// Se intentó guardar algo que otra solicitud acababa de crear: un día cerrado en la misma fecha, el horario
/// de un día que ya tiene o la fila única de configuración (choque con un índice único o la clave primaria).
/// </summary>
public class ConfiguracionDuplicadaException : Exception
{
    /// <summary>Crea la excepción con la causa original de la base de datos.</summary>
    public ConfiguracionDuplicadaException(string mensaje, Exception innerException) : base(mensaje, innerException)
    {
    }
}
