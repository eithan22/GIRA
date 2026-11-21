namespace GIRA.Domain.Entities.Analitica;

public sealed class EntrenamientoModelo
{
    public Guid Id { get; set; }
    public Guid SolicitadoPor { get; set; }
    public EstadoEntrenamientoModelo Estado { get; set; }
    public DateTimeOffset SolicitadoEn { get; set; }
    public DateTimeOffset? IniciadoEn { get; set; }
    public DateTimeOffset? FinalizadoEn { get; set; }
    public string? Error { get; set; }
    public List<ResultadoEntrenamientoModelo> Resultados { get; set; } = [];
}

public enum EstadoEntrenamientoModelo
{
    EnCola = 1,
    EnCurso = 2,
    Completado = 3,
    Fallido = 4
}

public sealed class ResultadoEntrenamientoModelo
{
    public Guid Id { get; set; }
    public Guid EntrenamientoId { get; set; }
    public Guid FranjaHorariaId { get; set; }
    public Guid? ModeloId { get; set; }
    public EstadoResultadoEntrenamiento Estado { get; set; }
    public int ObservacionesEntrenamiento { get; set; }
    public int ObservacionesValidacion { get; set; }
    public decimal? Mae { get; set; }
    public decimal? Rmse { get; set; }
    public decimal? MaeReferencia { get; set; }
    public string? VersionModelo { get; set; }
}

public enum EstadoResultadoEntrenamiento
{
    DatosInsuficientes = 1,
    Activado = 2,
    NoMejoraModeloVigente = 3
}

public sealed class ModeloAfluencia
{
    public Guid Id { get; set; }
    public Guid EntrenamientoId { get; set; }
    public Guid FranjaHorariaId { get; set; }
    public string Version { get; set; } = string.Empty;
    public DateOnly EntrenamientoDesde { get; set; }
    public DateOnly EntrenamientoHasta { get; set; }
    public DateOnly ValidacionDesde { get; set; }
    public DateOnly ValidacionHasta { get; set; }
    public decimal Mae { get; set; }
    public decimal Rmse { get; set; }
    public byte[] ArchivoModelo { get; set; } = [];
    public DateTimeOffset CreadoEn { get; set; }
    public bool Activo { get; set; }
}

public sealed class AuditoriaActivacionModelo
{
    public Guid Id { get; set; }
    public Guid? ModeloAnteriorId { get; set; }
    public Guid ModeloActivadoId { get; set; }
    public Guid UsuarioId { get; set; }
    public DateTimeOffset ActivadoEn { get; set; }
    public bool EsRollback { get; set; }
    public string Motivo { get; set; } = string.Empty;
}
