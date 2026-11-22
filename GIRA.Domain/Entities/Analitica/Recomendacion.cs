namespace GIRA.Domain.Entities.Analitica;

/// <summary>Recomendación de Analítica con estado de revisión y trazabilidad de su regla.</summary>
public sealed class Recomendacion
{
    public Guid Id { get; set; }
    public string Clave { get; set; } = string.Empty;
    public DateOnly Fecha { get; set; }
    public string Tipo { get; set; } = string.Empty;
    public string Mensaje { get; set; } = string.Empty;
    public string Prioridad { get; set; } = string.Empty;
    public string Estado { get; set; } = "Nueva";
    public string Origen { get; set; } = string.Empty;
    public DateTimeOffset FechaGeneracion { get; set; }
    public bool Vigente { get; set; }
}
