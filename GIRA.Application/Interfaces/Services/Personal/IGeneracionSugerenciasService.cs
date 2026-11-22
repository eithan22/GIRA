using GIRA.Application.Events.Analitica;

namespace GIRA.Application.Interfaces.Services.Personal;

/// <summary>Genera las sugerencias de personal a partir de una predicción de afluencia (GI-303, RF-PER-09).</summary>
public interface IGeneracionSugerenciasService
{
    /// <summary>
    /// Compara el personal requerido con el planificado en cada franja predicha. Crea sugerencias donde la diferencia
    /// alcanza el umbral, y pasa a Vencida las pendientes de las mismas fechas (RN-PER-07). No cambia turnos (RN-PER-06).
    /// </summary>
    Task GenerarDesdePrediccionAsync(PrediccionAfluenciaGeneradaEvent evento, CancellationToken cancellationToken);
}
