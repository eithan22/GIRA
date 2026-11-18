using GIRA.Domain.Base;

namespace GIRA.Domain.Entities.Inventario;

/// <summary>
/// Comensales que Analítica predice para una fecha (suma de sus franjas). Inventario la guarda al recibir
/// PrediccionAfluenciaGeneradaEvent para no leer datos de Analítica; única por fecha.
/// </summary>
public class AfluenciaPredicha : BaseEntity
{
    /// <summary>Fecha predicha (hora local del restaurante).</summary>
    public DateOnly Fecha { get; set; }

    /// <summary>Comensales esperados ese día.</summary>
    public int Comensales { get; set; }

    /// <summary>Cuándo llegó la predicción, en UTC.</summary>
    public DateTime FechaRecepcion { get; set; }
}
