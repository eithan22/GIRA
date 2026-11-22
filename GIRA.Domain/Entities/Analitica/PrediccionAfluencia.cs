namespace GIRA.Domain.Entities.Analitica;

/// <summary>Resultado de una generación de afluencia para una fecha y franja.</summary>
public sealed class PrediccionAfluencia
{
    public Guid Id { get; set; }
    public Guid GeneracionId { get; set; }
    public DateOnly Fecha { get; set; }
    public Guid FranjaHorariaId { get; set; }
    public int AfluenciaEstimada { get; set; }
    public decimal? LimiteInferior { get; set; }
    public decimal? LimiteSuperior { get; set; }
    public bool BajaConfianza { get; set; }
    public DateTimeOffset GeneradaEn { get; set; }
    public string VersionModelo { get; set; } = string.Empty;
    public bool Vigente { get; set; }
}
