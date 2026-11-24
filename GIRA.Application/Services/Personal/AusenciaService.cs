using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Application.Interfaces.Services.Personal;
using GIRA.Domain.Entities.Personal;
using GIRA.Domain.Enums.Personal;

namespace GIRA.Application.Services.Personal;

/// <summary>Implementación de <see cref="IAusenciaService"/>: valida, revisa el empleado y resuelve solicitudes pendientes.</summary>
public class AusenciaService : IAusenciaService
{
    private const string EmpleadoNoEncontrado = "El empleado no existe.";
    private const string NoEncontrada = "La solicitud de ausencia no existe.";
    private const string NoPendiente = "Solo se puede aprobar o rechazar una solicitud pendiente.";
    private const string EstadoNoPermitido = "Solo se puede aprobar o rechazar una solicitud.";

    private readonly IAusenciaRepository _ausencias;
    private readonly IEmpleadoRepository _empleados;
    private readonly IValidator<AusenciaRequest> _validador;

    /// <summary>Crea el servicio con los repositorios de ausencias y empleados y el validador.</summary>
    public AusenciaService(IAusenciaRepository ausencias, IEmpleadoRepository empleados, IValidator<AusenciaRequest> validador)
    {
        _ausencias = ausencias;
        _empleados = empleados;
        _validador = validador;
    }

    /// <inheritdoc />
    public async Task<OperationResult<IReadOnlyList<AusenciaDto>>> ListarAsync(Guid? empleadoId, CancellationToken cancellationToken)
    {
        var lista = await _ausencias.ListarAsync(empleadoId, cancellationToken);
        return OperationResult.Success<IReadOnlyList<AusenciaDto>>(lista.Select(ToDto).ToList());
    }

    /// <inheritdoc />
    public async Task<OperationResult<AusenciaDto>> CrearAsync(AusenciaRequest request, CancellationToken cancellationToken)
    {
        var validacion = await _validador.ValidateAsync(request, cancellationToken);
        if (!validacion.IsValid)
        {
            var mensaje = string.Join(" ", validacion.Errors.Select(e => e.ErrorMessage));
            return OperationResult.Failure<AusenciaDto>(ErrorType.Validation, mensaje);
        }

        if (await _empleados.BuscarAsync(request.EmpleadoId, cancellationToken) is null)
        {
            return OperationResult.Failure<AusenciaDto>(ErrorType.NotFound, EmpleadoNoEncontrado);
        }

        var ausencia = new Ausencia
        {
            EmpleadoId = request.EmpleadoId,
            FechaDesde = request.FechaDesde,
            FechaHasta = request.FechaHasta,
            Tipo = request.Tipo,
            Motivo = string.IsNullOrWhiteSpace(request.Motivo) ? null : request.Motivo.Trim(),
            Estado = EstadoAusencia.Solicitada,
        };
        _ausencias.Agregar(ausencia);
        await _ausencias.GuardarCambiosAsync(cancellationToken);

        return OperationResult.Success(ToDto(ausencia));
    }

    /// <inheritdoc />
    public async Task<OperationResult<AusenciaDto>> ResolverAsync(Guid id, EstadoAusencia estado, Guid resueltaPor, CancellationToken cancellationToken)
    {
        if (estado is not (EstadoAusencia.Aprobada or EstadoAusencia.Rechazada))
        {
            return OperationResult.Failure<AusenciaDto>(ErrorType.Validation, EstadoNoPermitido);
        }

        var ausencia = await _ausencias.BuscarAsync(id, cancellationToken);
        if (ausencia is null)
        {
            return OperationResult.Failure<AusenciaDto>(ErrorType.NotFound, NoEncontrada);
        }

        if (ausencia.Estado != EstadoAusencia.Solicitada)
        {
            return OperationResult.Failure<AusenciaDto>(ErrorType.Conflict, NoPendiente);
        }

        ausencia.Estado = estado;
        ausencia.AprobadaPor = resueltaPor;
        await _ausencias.GuardarCambiosAsync(cancellationToken);

        return OperationResult.Success(ToDto(ausencia));
    }

    /// <summary>Mapeo manual de la entidad al DTO.</summary>
    private static AusenciaDto ToDto(Ausencia a) =>
        new(a.Id, a.EmpleadoId, a.FechaDesde, a.FechaHasta, a.Tipo, a.Motivo, a.Estado, a.AprobadaPor);
}
