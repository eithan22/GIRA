using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Application.Interfaces.Services.Personal;
using GIRA.Domain.Entities.Personal;

namespace GIRA.Application.Services.Personal;

/// <summary>Implementación de <see cref="IRequerimientoPersonalService"/>: valida, revisa que el cargo exista y guarda (crea o actualiza).</summary>
public class RequerimientoPersonalService : IRequerimientoPersonalService
{
    private const string CargoInexistente = "El cargo indicado no existe.";

    private readonly IRequerimientoPersonalRepository _requerimientos;
    private readonly ICargoRepository _cargos;
    private readonly IValidator<RequerimientoPersonalRequest> _validador;

    /// <summary>Crea el servicio con los repositorios de requerimientos y cargos y el validador.</summary>
    public RequerimientoPersonalService(
        IRequerimientoPersonalRepository requerimientos,
        ICargoRepository cargos,
        IValidator<RequerimientoPersonalRequest> validador)
    {
        _requerimientos = requerimientos;
        _cargos = cargos;
        _validador = validador;
    }

    /// <inheritdoc />
    public async Task<OperationResult<IReadOnlyList<RequerimientoPersonalDto>>> ListarAsync(CancellationToken cancellationToken)
    {
        var lista = await _requerimientos.ListarAsync(cancellationToken);
        return OperationResult.Success<IReadOnlyList<RequerimientoPersonalDto>>(lista.Select(ToDto).ToList());
    }

    /// <inheritdoc />
    public async Task<OperationResult<RequerimientoPersonalDto>> GuardarAsync(RequerimientoPersonalRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _validador.ValidateAsync(request, cancellationToken);
        if (!resultado.IsValid)
        {
            var mensaje = string.Join(" ", resultado.Errors.Select(e => e.ErrorMessage));
            return OperationResult.Failure<RequerimientoPersonalDto>(ErrorType.Validation, mensaje);
        }

        if (await _cargos.BuscarAsync(request.CargoId, cancellationToken) is null)
        {
            return OperationResult.Failure<RequerimientoPersonalDto>(ErrorType.Validation, CargoInexistente);
        }

        var requerimiento = await _requerimientos.BuscarPorCargoAsync(request.CargoId, cancellationToken);
        if (requerimiento is null)
        {
            requerimiento = new RequerimientoPersonal { CargoId = request.CargoId };
            _requerimientos.Agregar(requerimiento);
        }

        requerimiento.ComensalesPorEmpleado = request.ComensalesPorEmpleado;
        requerimiento.MinimoPorFranja = request.MinimoPorFranja;
        await _requerimientos.GuardarCambiosAsync(cancellationToken);

        return OperationResult.Success(ToDto(requerimiento));
    }

    /// <summary>Mapeo manual de la entidad al DTO.</summary>
    private static RequerimientoPersonalDto ToDto(RequerimientoPersonal r) =>
        new(r.Id, r.CargoId, r.ComensalesPorEmpleado, r.MinimoPorFranja);
}
