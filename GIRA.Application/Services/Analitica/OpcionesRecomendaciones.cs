namespace GIRA.Application.Services.Analitica;

/// <summary>Umbrales de las reglas de recomendaciones, configurables en el servidor.</summary>
public sealed class OpcionesRecomendaciones
{
    public decimal PorcentajeAumentoDemanda { get; set; } = 30;
    public decimal PorcentajeNoShow { get; set; } = 15;
    public int AlertasMensuales { get; set; } = 2;
}
