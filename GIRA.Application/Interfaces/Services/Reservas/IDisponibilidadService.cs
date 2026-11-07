using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;

namespace GIRA.Application.Interfaces.Services.Reservas;

/// <summary>Consulta qué franjas tienen al menos una mesa disponible para una fecha y cantidad de personas.</summary>
public interface IDisponibilidadService
{
    /// <summary>Devuelve las franjas disponibles, o un error de validación si los datos no son válidos.</summary>
    Task<OperationResult<IReadOnlyList<FranjaDisponibleDto>>> ConsultarAsync(
        ConsultarDisponibilidadRequest request, CancellationToken cancellationToken);
}
