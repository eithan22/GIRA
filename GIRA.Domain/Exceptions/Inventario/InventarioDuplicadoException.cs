namespace GIRA.Domain.Exceptions.Inventario;

/// <summary>La base rechazó un nombre repetido (índice único) de un catálogo, insumo o plato.</summary>
public class InventarioDuplicadoException : Exception
{
    /// <summary>Crea la excepción con el error original de la base.</summary>
    public InventarioDuplicadoException(string mensaje, Exception innerException) : base(mensaje, innerException) { }
}
