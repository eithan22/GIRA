using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Application.Interfaces.Services.Personal;
using GIRA.Domain.Entities.Personal;
using GIRA.Domain.Exceptions.Personal;

namespace GIRA.Application.Services.Personal;

/// <summary>Implementación de <see cref="IEmpleadoService"/>: valida, revisa cédula y cargo, y guarda.</summary>
public class EmpleadoService : IEmpleadoService
{
    private const string CedulaDuplicada = "Ya existe un empleado con esa cédula.";
    private const string CargoInexistente = "El cargo indicado no existe.";
    private const string NoEncontrado = "El empleado no existe.";

    private readonly IEmpleadoRepository _empleados;
    private readonly ICargoRepository _cargos;
    private readonly IValidator<EmpleadoRequest> _validador;

    /// <summary>Crea el servicio con los repositorios de empleados y cargos y el validador de datos.</summary>
    public EmpleadoService(IEmpleadoRepository empleados, ICargoRepository cargos, IValidator<EmpleadoRequest> validador)
    {
        _empleados = empleados;
        _cargos = cargos;
        _validador = validador;
    }

    /// <inheritdoc />
    public async Task<OperationResult<IReadOnlyList<EmpleadoDto>>> ListarAsync(bool soloActivos, CancellationToken cancellationToken)
    {
        var empleados = await _empleados.ListarAsync(soloActivos, cancellationToken);
        return OperationResult.Success<IReadOnlyList<EmpleadoDto>>(empleados.Select(ToDto).ToList());
    }

    /// <inheritdoc />
    public async Task<OperationResult<EmpleadoDto>> CrearAsync(EmpleadoRequest request, CancellationToken cancellationToken)
    {
        var validacion = await ValidarAsync(request, cancellationToken);
        if (validacion is not null)
        {
            return validacion;
        }

        var error = await RevisarCargoYCedulaAsync(request, null, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var empleado = new Empleado { Activo = true };
        Copiar(request, empleado);
        _empleados.Agregar(empleado);

        return await GuardarAsync(empleado, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OperationResult<EmpleadoDto>> ActualizarAsync(Guid id, EmpleadoRequest request, CancellationToken cancellationToken)
    {
        var validacion = await ValidarAsync(request, cancellationToken);
        if (validacion is not null)
        {
            return validacion;
        }

        var empleado = await _empleados.BuscarAsync(id, cancellationToken);
        if (empleado is null)
        {
            return OperationResult.Failure<EmpleadoDto>(ErrorType.NotFound, NoEncontrado);
        }

        var error = await RevisarCargoYCedulaAsync(request, id, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        Copiar(request, empleado);
        return await GuardarAsync(empleado, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<OperationResult<EmpleadoDto>> CambiarEstadoAsync(Guid id, bool activo, CancellationToken cancellationToken)
    {
        var empleado = await _empleados.BuscarAsync(id, cancellationToken);
        if (empleado is null)
        {
            return OperationResult.Failure<EmpleadoDto>(ErrorType.NotFound, NoEncontrado);
        }

        empleado.Activo = activo;
        return await GuardarAsync(empleado, cancellationToken);
    }

    private async Task<OperationResult<EmpleadoDto>?> ValidarAsync(EmpleadoRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _validador.ValidateAsync(request, cancellationToken);
        if (resultado.IsValid)
        {
            return null;
        }

        var mensaje = string.Join(" ", resultado.Errors.Select(e => e.ErrorMessage));
        return OperationResult.Failure<EmpleadoDto>(ErrorType.Validation, mensaje);
    }

    private async Task<OperationResult<EmpleadoDto>?> RevisarCargoYCedulaAsync(EmpleadoRequest request, Guid? excluirId, CancellationToken cancellationToken)
    {
        if (await _cargos.BuscarAsync(request.CargoId, cancellationToken) is null)
        {
            return OperationResult.Failure<EmpleadoDto>(ErrorType.Validation, CargoInexistente);
        }

        if (await _empleados.ExisteCedulaAsync(request.Cedula.Trim(), excluirId, cancellationToken))
        {
            return OperationResult.Failure<EmpleadoDto>(ErrorType.Conflict, CedulaDuplicada);
        }

        return null;
    }

    private async Task<OperationResult<EmpleadoDto>> GuardarAsync(Empleado empleado, CancellationToken cancellationToken)
    {
        try
        {
            await _empleados.GuardarCambiosAsync(cancellationToken);
        }
        catch (EmpleadoCedulaDuplicadaException)
        {
            return OperationResult.Failure<EmpleadoDto>(ErrorType.Conflict, CedulaDuplicada);
        }

        return OperationResult.Success(ToDto(empleado));
    }

    private static void Copiar(EmpleadoRequest request, Empleado empleado)
    {
        empleado.Nombre = request.Nombre.Trim();
        empleado.Apellido = request.Apellido.Trim();
        empleado.Cedula = request.Cedula.Trim();
        empleado.Telefono = Normalizar(request.Telefono);
        empleado.Email = Normalizar(request.Email);
        empleado.CargoId = request.CargoId;
        empleado.FechaIngreso = request.FechaIngreso;
        empleado.HorasMaximasSemana = request.HorasMaximasSemana;
    }

    private static string? Normalizar(string? valor) =>
        string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();

    /// <summary>Mapeo manual de la entidad al DTO.</summary>
    private static EmpleadoDto ToDto(Empleado e) =>
        new(e.Id, e.Nombre, e.Apellido, e.Cedula, e.Telefono, e.Email, e.CargoId, e.FechaIngreso, e.HorasMaximasSemana, e.Activo);
}
