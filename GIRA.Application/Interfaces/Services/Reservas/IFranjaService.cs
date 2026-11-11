using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using System;
using System.Collections.Generic;
using System.Text;

namespace GIRA.Application.Interfaces.Services.Reservas;


public interface IFranjaService
{
    Task<OperationResult<PagedResult<FranjaDto>>> ListarAsync(ListarSalonRequest request, CancellationToken cancellationToken);
    Task<OperationResult<FranjaDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken);
    Task<OperationResult<FranjaDto>> CrearAsync(GuardarFranjaRequest request, CancellationToken cancellationToken);
    Task<OperationResult<FranjaDto>> ActualizarAsync(Guid id, GuardarFranjaRequest request, CancellationToken cancellationToken);
    Task<OperationResult<FranjaDto>> CambiarEstadoAsync(Guid id, bool activa, CancellationToken cancellationToken);
}

