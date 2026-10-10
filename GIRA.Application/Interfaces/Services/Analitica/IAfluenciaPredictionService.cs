namespace GIRA.Application.Interfaces.Services.Analitica;

/// <summary>Genera y persiste pronósticos de afluencia para los próximos siete días.</summary>
public interface IAfluenciaPredictionService
{
    /// <summary>Entrena SSA con el historial disponible y reemplaza los pronósticos futuros.</summary>
    Task GenerarPrediccionesAsync(CancellationToken cancellationToken);
}
