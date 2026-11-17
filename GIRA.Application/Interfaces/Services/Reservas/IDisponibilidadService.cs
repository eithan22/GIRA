using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;

namespace GIRA.Application.Interfaces.Services.Reservas;

/// <summary>Consulta qué franjas tienen al menos una mesa disponible para una fecha y cantidad de personas.</summary>
public interface IDisponibilidadService
{
    /// <summary>
    /// Devuelve las franjas disponibles, o un error de validación si los datos no son válidos. Con
    /// <see cref="AudienciaReserva.Cliente"/> (valor por defecto) se excluyen las franjas a menos de la
    /// antelación mínima; con <see cref="AudienciaReserva.Personal"/> solo las que ya terminaron.
    /// </summary>
    Task<OperationResult<IReadOnlyList<FranjaDisponibleDto>>> ConsultarAsync(
        ConsultarDisponibilidadRequest request, CancellationToken cancellationToken,
        AudienciaReserva audiencia = AudienciaReserva.Cliente);
}
