using GIRA.Application.Common;
using GIRA.Application.DTOs.Personal;

namespace GIRA.Application.Interfaces.Services.Personal;

/// <summary>Planificación semanal con cobertura por día, franja y cargo (GI-301, RF-PER-07).</summary>
public interface IPlanificacionService
{
    /// <summary>
    /// Devuelve, por cada día, franja activa y cargo, cuántos turnos Programados o Confirmados cubren la franja completa.
    /// Incluye también las combinaciones sin turnos (cero), para que la cuadrícula esté completa.
    /// Si <paramref name="cargoId"/> viene, solo devuelve ese cargo.
    /// </summary>
    Task<OperationResult<IReadOnlyList<CoberturaFranjaDto>>> CoberturaAsync(DateOnly desde, DateOnly hasta, Guid? cargoId, CancellationToken cancellationToken);
}
