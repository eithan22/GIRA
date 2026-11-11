using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using System;
using System.Collections.Generic;
using System.Text;

namespace GIRA.Application.Interfaces.Services.Reservas;

public interface IZonaService
{
    Task<OperationResult<PagedResult<ZonaDto>>> ListarAsync(ListarSalonRequest request, CancellationToken cancellationToken);
    Task<OperationResult<ZonaDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken);
    Task<OperationResult<ZonaDto>> CrearAsync(GuardarZonaRequest request, CancellationToken cancellationToken);
    Task<OperationResult<ZonaDto>> ActualizarAsync(Guid id, GuardarZonaRequest request, CancellationToken cancellationToken);
    Task<OperationResult<ZonaDto>> CambiarEstadoAsync(Guid id, bool activa, CancellationToken cancellationToken);
}
