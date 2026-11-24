using GIRA.Application.Common;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Application.Interfaces.Services.Personal;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Domain.Entities.Personal;
using GIRA.Domain.Enums.Personal;

namespace GIRA.Application.Services.Personal;

/// <summary>Implementación de <see cref="ISugerenciaService"/> (GI-304, RF-PER-10).</summary>
public class SugerenciaService : ISugerenciaService
{
    private const string NoEncontrada = "La sugerencia no existe.";
    private const string EstadoNoPermitido = "Solo se puede resolver como Aceptada o Rechazada.";
    private const string YaResuelta = "Solo se resuelven sugerencias pendientes.";

    private readonly ISugerenciaTurnoRepository _sugerencias;
    private readonly ICargoRepository _cargos;
    private readonly IFranjaService _franjas;

    /// <summary>Crea el servicio con el repositorio de sugerencias, los cargos y las franjas de Reservas.</summary>
    public SugerenciaService(ISugerenciaTurnoRepository sugerencias, ICargoRepository cargos, IFranjaService franjas)
    {
        _sugerencias = sugerencias;
        _cargos = cargos;
        _franjas = franjas;
    }

    /// <inheritdoc />
    public async Task<OperationResult<IReadOnlyList<SugerenciaDto>>> ListarAsync(EstadoSugerencia? estado, CancellationToken cancellationToken)
    {
        var sugerencias = await _sugerencias.ListarAsync(estado, cancellationToken);
        var nombres = await CargarNombresAsync(cancellationToken);
        if (nombres.Error is not null)
        {
            return OperationResult.Failure<IReadOnlyList<SugerenciaDto>>(ErrorType.Validation, nombres.Error);
        }

        return OperationResult.Success<IReadOnlyList<SugerenciaDto>>(
            sugerencias.Select(s => ToDto(s, nombres)).ToList());
    }

    /// <inheritdoc />
    public async Task<OperationResult<SugerenciaDto>> ResolverAsync(Guid id, EstadoSugerencia nuevoEstado, Guid usuarioId, CancellationToken cancellationToken)
    {
        if (nuevoEstado is not (EstadoSugerencia.Aceptada or EstadoSugerencia.Rechazada))
        {
            return OperationResult.Failure<SugerenciaDto>(ErrorType.Validation, EstadoNoPermitido);
        }

        var sugerencia = await _sugerencias.BuscarAsync(id, cancellationToken);
        if (sugerencia is null)
        {
            return OperationResult.Failure<SugerenciaDto>(ErrorType.NotFound, NoEncontrada);
        }

        if (sugerencia.Estado != EstadoSugerencia.Pendiente)
        {
            return OperationResult.Failure<SugerenciaDto>(ErrorType.Conflict, YaResuelta);
        }

        sugerencia.Estado = nuevoEstado;
        sugerencia.ResueltaPor = usuarioId;
        await _sugerencias.GuardarCambiosAsync(cancellationToken);

        var nombres = await CargarNombresAsync(cancellationToken);
        if (nombres.Error is not null)
        {
            return OperationResult.Failure<SugerenciaDto>(ErrorType.Validation, nombres.Error);
        }

        return OperationResult.Success(ToDto(sugerencia, nombres));
    }

    /// <summary>Carga los nombres de cargos y franjas que se muestran junto a cada sugerencia.</summary>
    private async Task<Nombres> CargarNombresAsync(CancellationToken cancellationToken)
    {
        var cargos = (await _cargos.ListarAsync(cancellationToken)).ToDictionary(c => c.Id, c => c.Nombre);
        var franjasResultado = await _franjas.ListarOperativasAsync(cancellationToken);
        if (franjasResultado.IsFailure)
        {
            return new Nombres(cargos, new Dictionary<Guid, GIRA.Application.DTOs.Reservas.FranjaOperativaDto>(), franjasResultado.Error ?? "No se pudieron leer las franjas horarias.");
        }

        return new Nombres(cargos, franjasResultado.Value!.ToDictionary(f => f.Id), null);
    }

    private static SugerenciaDto ToDto(SugerenciaTurno s, Nombres nombres)
    {
        nombres.Franjas.TryGetValue(s.FranjaHorariaId, out var franja);
        nombres.Cargos.TryGetValue(s.CargoId, out var cargo);
        return new SugerenciaDto(
            s.Id,
            s.Fecha,
            s.FranjaHorariaId,
            franja?.Nombre ?? "Franja desconocida",
            franja?.HoraInicio ?? default,
            franja?.HoraFin ?? default,
            s.CargoId,
            cargo ?? "Cargo desconocido",
            s.PersonalPlanificado,
            s.PersonalRequerido,
            s.Diferencia,
            s.Estado,
            s.FechaGeneracion,
            s.ResueltaPor);
    }

    private sealed record Nombres(
        IReadOnlyDictionary<Guid, string> Cargos,
        IReadOnlyDictionary<Guid, GIRA.Application.DTOs.Reservas.FranjaOperativaDto> Franjas,
        string? Error);
}
