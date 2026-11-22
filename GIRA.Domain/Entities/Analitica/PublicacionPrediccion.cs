namespace GIRA.Domain.Entities.Analitica;

/// <summary>Entrega pendiente de un día completo de predicciones a otros módulos.</summary>
public sealed class PublicacionPrediccion
{
    public Guid Id { get; set; }
    public Guid GeneracionId { get; set; }
    public DateOnly Fecha { get; set; }
    public DateTimeOffset? PublicadaEn { get; set; }
}
