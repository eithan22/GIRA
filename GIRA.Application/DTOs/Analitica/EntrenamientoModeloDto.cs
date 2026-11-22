namespace GIRA.Application.DTOs.Analitica;

public sealed record EntrenamientoModeloSolicitadoDto(Guid Id, string Estado);

public sealed record EntrenamientoModeloDto(
    Guid Id,
    string Estado,
    DateTimeOffset SolicitadoEn,
    DateTimeOffset? IniciadoEn,
    DateTimeOffset? FinalizadoEn,
    string? Error,
    IReadOnlyList<ResultadoEntrenamientoModeloDto> Resultados);

public sealed record ResultadoEntrenamientoModeloDto(
    Guid FranjaHorariaId,
    string Franja,
    string Estado,
    int ObservacionesEntrenamiento,
    int ObservacionesValidacion,
    decimal? Mae,
    decimal? Rmse,
    decimal? MaeReferencia,
    string? VersionModelo);

public sealed record ModeloAfluenciaDto(
    Guid Id,
    Guid FranjaHorariaId,
    string Franja,
    string Version,
    DateOnly EntrenamientoDesde,
    DateOnly EntrenamientoHasta,
    DateOnly ValidacionDesde,
    DateOnly ValidacionHasta,
    decimal Mae,
    decimal Rmse,
    DateTimeOffset CreadoEn,
    bool Activo);

public sealed record ActivarModeloRequest(string Motivo);

public enum ResultadoActivacionModelo
{
    Activado,
    YaActivo,
    NoEncontrado,
    NoEsRollback
}
