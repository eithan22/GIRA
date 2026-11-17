using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Infrastructure.Persistence;

/// <summary>
/// Detecta si una <see cref="DbUpdateException"/> de SQL Server es la violación de un índice
/// único específico, por su número de error (2601 o 2627) y el nombre del índice en el mensaje.
/// Se usa en los repositorios para traducir errores de la base a excepciones de negocio.
/// </summary>
internal static class ViolacionesIndiceUnico
{
    /// <summary>True si <paramref name="excepcion"/> es la violación del índice <paramref name="nombreIndice"/>.</summary>
    public static bool Es(DbUpdateException excepcion, string nombreIndice)
    {
        return excepcion.InnerException is SqlException sqlException
            && (sqlException.Number == 2601 || sqlException.Number == 2627)
            && sqlException.Message.Contains(nombreIndice, StringComparison.OrdinalIgnoreCase);
    }
}
