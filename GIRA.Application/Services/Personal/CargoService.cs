using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Application.Interfaces.Services.Personal;
using GIRA.Domain.Entities.Personal;
using GIRA.Domain.Exceptions.Personal;

namespace GIRA.Application.Services.Personal;

/// <summary>Implementación de <see cref="ICargoService"/>: valida, revisa la unicidad del nombre y guarda.</summary>
public class CargoService : ICargoService
{
    private const string NombreDuplicado = "Ya existe un cargo con ese nombre.";

    private readonly ICargoRepository _repositorio;
    private readonly IValidator<CargoRequest> _validador;

    /// <summary>Crea el servicio con el repositorio de cargos y el validador de datos.</summary>
    public CargoService(ICargoRepository repositorio, IValidator<CargoRequest> validador)
    {
        _repositorio = repositorio;
        _validador = validador;
    }

    /// <inheritdoc />
    public async Task<OperationResult<IReadOnlyList<CargoDto>>> ListarAsync(CancellationToken cancellationToken)
    {
        var cargos = await _repositorio.ListarAsync(cancellationToken);
        return OperationResult.Success<IReadOnlyList<CargoDto>>(cargos.Select(ToDto).ToList());
    }

    /// <inheritdoc />
    public async Task<OperationResult<CargoDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken)
    {
        var cargo = await _repositorio.BuscarAsync(id, cancellationToken);
        return cargo is null
            ? OperationResult.Failure<CargoDto>(ErrorType.NotFound, "El cargo no existe.")
            : OperationResult.Success(ToDto(cargo));
    }

    /// <inheritdoc />
    public async Task<OperationResult<CargoDto>> CrearAsync(CargoRequest request, CancellationToken cancellationToken)
    {
        var validacion = await ValidarAsync(request, cancellationToken);
        if (validacion is not null)
        {
            return validacion;
        }

        var nombre = request.Nombre.Trim();
        if (await _repositorio.ExisteNombreAsync(nombre, null, cancellationToken))
        {
            return OperationResult.Failure<CargoDto>(ErrorType.Conflict, NombreDuplicado);
        }

        var cargo = new Cargo { Nombre = nombre, Descripcion = Normalizar(request.Descripcion) };
        _repositorio.Agregar(cargo);

        try
        {
            await _repositorio.GuardarCambiosAsync(cancellationToken);
        }
        catch (CargoNombreDuplicadoException)
        {
            return OperationResult.Failure<CargoDto>(ErrorType.Conflict, NombreDuplicado);
        }

        return OperationResult.Success(ToDto(cargo));
    }

    /// <inheritdoc />
    public async Task<OperationResult<CargoDto>> ActualizarAsync(Guid id, CargoRequest request, CancellationToken cancellationToken)
    {
        var validacion = await ValidarAsync(request, cancellationToken);
        if (validacion is not null)
        {
            return validacion;
        }

        var cargo = await _repositorio.BuscarAsync(id, cancellationToken);
        if (cargo is null)
        {
            return OperationResult.Failure<CargoDto>(ErrorType.NotFound, "El cargo no existe.");
        }

        var nombre = request.Nombre.Trim();
        if (await _repositorio.ExisteNombreAsync(nombre, id, cancellationToken))
        {
            return OperationResult.Failure<CargoDto>(ErrorType.Conflict, NombreDuplicado);
        }

        cargo.Nombre = nombre;
        cargo.Descripcion = Normalizar(request.Descripcion);

        try
        {
            await _repositorio.GuardarCambiosAsync(cancellationToken);
        }
        catch (CargoNombreDuplicadoException)
        {
            return OperationResult.Failure<CargoDto>(ErrorType.Conflict, NombreDuplicado);
        }

        return OperationResult.Success(ToDto(cargo));
    }

    private async Task<OperationResult<CargoDto>?> ValidarAsync(CargoRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _validador.ValidateAsync(request, cancellationToken);
        if (resultado.IsValid)
        {
            return null;
        }

        var mensaje = string.Join(" ", resultado.Errors.Select(e => e.ErrorMessage));
        return OperationResult.Failure<CargoDto>(ErrorType.Validation, mensaje);
    }

    private static string? Normalizar(string? descripcion) =>
        string.IsNullOrWhiteSpace(descripcion) ? null : descripcion.Trim();

    /// <summary>Mapeo manual de la entidad al DTO.</summary>
    private static CargoDto ToDto(Cargo cargo) => new(cargo.Id, cargo.Nombre, cargo.Descripcion);
}
