namespace GIRA.Domain.Entities.Analitica;

/// <summary>Generación diaria persistida, incluso cuando no hay datos para pronosticar.</summary>
public sealed class GeneracionPrediccion
{
    public Guid Id { get; set; }
    public DateOnly FechaProceso { get; set; }
    public DateTimeOffset GeneradaEn { get; set; }
    public int CantidadPredicciones { get; set; }
}
