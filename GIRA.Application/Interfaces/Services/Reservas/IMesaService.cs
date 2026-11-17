using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using System;
using System.Collections.Generic;
using System.Text;

namespace GIRA.Application.Interfaces.Services.Reservas;


public interface IMesaService
{
    Task<OperationResult<PagedResult<MesaDto>>> ListarAsync(ListarMesasRequest request, CancellationToken cancellationToken);
    Task<OperationResult<MesaDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken);
    Task<OperationResult<MesaDto>> CrearAsync(GuardarMesaRequest request, CancellationToken cancellationToken);
    Task<OperationResult<MesaDto>> ActualizarAsync(Guid id, GuardarMesaRequest request, CancellationToken cancellationToken);
    Task<OperationResult<MesaDto>> CambiarEstadoAsync(Guid id, bool activa, CancellationToken cancellationToken);
}