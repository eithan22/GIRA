using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;

namespace GIRA.Application.Interfaces.Services.Reservas;

/// <summary>Casos de uso del mapa de ocupación de mesas (GI-167).</summary>
public interface IOcupacionService
{
    /// <summary>
    /// Mapa de todas las mesas por zona con su estado (Libre, Reservada, Ocupada o Inactiva) para una fecha y una
    /// franja (RF-RES-14). Una reserva EnCurso es Ocupada y una Confirmada es Reservada; una mesa inactiva (o de
    /// zona inactiva) es Inactiva aunque tenga reserva (el mapa igual muestra esa reserva). Las visitas sin reserva
    /// no cuentan: no tienen hora de salida. ErrorType.Validation si la franja falta o la fecha no es válida y
    /// ErrorType.NotFound si la franja no existe.
    /// </summary>
    Task<OperationResult<MapaOcupacionDto>> ObtenerMapaAsync(ConsultarOcupacionRequest request, CancellationToken cancellationToken);
}
