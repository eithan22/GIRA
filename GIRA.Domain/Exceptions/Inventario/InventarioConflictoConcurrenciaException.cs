namespace GIRA.Domain.Exceptions.Inventario;

/// <summary>Otra persona cambió el mismo registro mientras se guardaba (falla RowVersion).</summary>
public class InventarioConflictoConcurrenciaException : Exception
{
    /// <summary>Crea la excepción con el error original de la base.</summary>
    public InventarioConflictoConcurrenciaException(string mensaje, Exception innerException) : base(mensaje, innerException) { }
}
