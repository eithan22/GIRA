namespace GIRA.Domain.Entities.Analitica;

/// <summary>Capacidad de aforo diaria capturada antes de iniciar la operación del restaurante.</summary>
public sealed class CapacidadOperativaDiaria
{
    public Guid Id { get; set; }
    public DateOnly Fecha { get; set; }
    public int CapacidadTotal { get; set; }
    public DateTimeOffset CapturadaEn { get; set; }
}
