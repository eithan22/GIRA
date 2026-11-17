namespace GIRA.Domain.Exceptions.Configuracion;

/// <summary>Otra persona modificó la configuración mientras se editaba (falla el <c>RowVersion</c>).</summary>
public class ConfiguracionConflictoConcurrenciaException : Exception
{
    /// <summary>Crea la excepción con la causa original de la base de datos.</summary>
    public ConfiguracionConflictoConcurrenciaException(string mensaje, Exception innerException)
        : base(mensaje, innerException)
    {
    }
}
