using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Application.Interfaces.Services.Personal;
using GIRA.Domain.Entities.Personal;

namespace GIRA.Application.Services.Personal;

/// <summary>Implementación de <see cref="IDisponibilidadService"/>: valida, revisa el solapamiento y guarda.</summary>
public class DisponibilidadService : IDisponibilidadService
{
    private const string EmpleadoNoEncontrado = "El empleado no existe.";
    private const string Solapado = "El bloque se solapa con otro bloque de disponibilidad de ese día.";
    private const string NoEncontrado = "El bloque de disponibilidad no existe.";

    private readonly IDisponibilidadRepository _disponibilidades;
    private readonly IEmpleadoRepository _empleados;
    private readonly IValidator<DisponibilidadRequest> _validador;

    /// <summary>Crea el servicio con los repositorios de disponibilidad y empleados y el validador.</summary>
    public DisponibilidadService(
        IDisponibilidadRepository disponibilidades,
        IEmpleadoRepository empleados,
        IValidator<DisponibilidadRequest> validador)
    {
        _disponibilidades = disponibilidades;
        _empleados = empleados;
        _validador = validador;
    }

    /// <inheritdoc />
    public async Task<OperationResult<IReadOnlyList<DisponibilidadDto>>> ListarAsync(Guid empleadoId, CancellationToken cancellationToken)
    {
        if (await _empleados.BuscarAsync(empleadoId, cancellationToken) is null)
        {
            return OperationResult.Failure<IReadOnlyList<DisponibilidadDto>>(ErrorType.NotFound, EmpleadoNoEncontrado);
        }

        var bloques = await _disponibilidades.ListarPorEmpleadoAsync(empleadoId, cancellationToken);
        return OperationResult.Success<IReadOnlyList<DisponibilidadDto>>(bloques.Select(ToDto).ToList());
    }

    /// <inheritdoc />
    public async Task<OperationResult<DisponibilidadDto>> AgregarAsync(Guid empleadoId, DisponibilidadRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _validador.ValidateAsync(request, cancellationToken);
        if (!resultado.IsValid)
        {
            var mensaje = string.Join(" ", resultado.Errors.Select(e => e.ErrorMessage));
            return OperationResult.Failure<DisponibilidadDto>(ErrorType.Validation, mensaje);
        }

        if (await _empleados.BuscarAsync(empleadoId, cancellationToken) is null)
        {
            return OperationResult.Failure<DisponibilidadDto>(ErrorType.NotFound, EmpleadoNoEncontrado);
        }

        var existentes = await _disponibilidades.ListarPorEmpleadoAsync(empleadoId, cancellationToken);
        var solapa = existentes.Any(d =>
            d.DiaSemana == request.DiaSemana &&
            request.HoraInicio < d.HoraFin &&
            d.HoraInicio < request.HoraFin);
        if (solapa)
        {
            return OperationResult.Failure<DisponibilidadDto>(ErrorType.Conflict, Solapado);
        }

        var bloque = new Disponibilidad
        {
            EmpleadoId = empleadoId,
            DiaSemana = request.DiaSemana,
            HoraInicio = request.HoraInicio,
            HoraFin = request.HoraFin,
        };
        _disponibilidades.Agregar(bloque);
        await _disponibilidades.GuardarCambiosAsync(cancellationToken);

        return OperationResult.Success(ToDto(bloque));
    }

    /// <inheritdoc />
    public async Task<OperationResult> EliminarAsync(Guid empleadoId, Guid id, CancellationToken cancellationToken)
    {
        var bloque = await _disponibilidades.BuscarAsync(id, cancellationToken);
        if (bloque is null || bloque.EmpleadoId != empleadoId)
        {
            return OperationResult.Failure(ErrorType.NotFound, NoEncontrado);
        }

        _disponibilidades.Eliminar(bloque);
        await _disponibilidades.GuardarCambiosAsync(cancellationToken);

        return OperationResult.Success();
    }

    /// <summary>Mapeo manual de la entidad al DTO.</summary>
    private static DisponibilidadDto ToDto(Disponibilidad d) =>
        new(d.Id, d.EmpleadoId, d.DiaSemana, d.HoraInicio, d.HoraFin);
}
