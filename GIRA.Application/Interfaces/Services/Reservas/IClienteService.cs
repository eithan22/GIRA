using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;

namespace GIRA.Application.Interfaces.Services.Reservas;

/// <summary>Casos de uso de consulta de clientes para el personal (GI-164).</summary>
public interface IClienteService
{
    /// <summary>
    /// Busca clientes por parte del nombre o del teléfono, con paginación. ErrorType.Validation
    /// si el texto tiene menos de 3 caracteres o la paginación no es válida.
    /// </summary>
    Task<OperationResult<PagedResult<ClienteDto>>> BuscarAsync(BuscarClientesRequest request, CancellationToken cancellationToken);
}
