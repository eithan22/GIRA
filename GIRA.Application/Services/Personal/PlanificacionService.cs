using GIRA.Application.Common;
using GIRA.Application.Common.Personal;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Application.Interfaces.Services.Personal;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Domain.Enums.Personal;

namespace GIRA.Application.Services.Personal;

/// <summary>
/// Implementación de <see cref="IPlanificacionService"/> (GI-301, RF-PER-07). Cuenta por día, franja y cargo los turnos
/// Programados o Confirmados que cubren la franja completa, según la regla de planificación del SRS.
/// Las franjas se leen del módulo de Reservas por su contrato público (<see cref="IFranjaService"/>), no por sus entidades.
/// </summary>
public class PlanificacionService : IPlanificacionService
{
    /// <summary>Máximo de días que se consultan de una vez, para no devolver cuadrículas enormes.</summary>
    public const int MaximoDias = 31;

    private const string RangoInvertido = "La fecha de inicio no puede ser posterior a la fecha de fin.";
    private const string RangoMuyGrande = "El rango no puede superar 31 días.";
    private const string CargoNoEncontrado = "El cargo indicado no existe.";

    private readonly ITurnoRepository _turnos;
    private readonly ICargoRepository _cargos;
    private readonly IFranjaService _franjas;

    /// <summary>Crea el servicio con los turnos, los cargos y las franjas de Reservas.</summary>
    public PlanificacionService(ITurnoRepository turnos, ICargoRepository cargos, IFranjaService franjas)
    {
        _turnos = turnos;
        _cargos = cargos;
        _franjas = franjas;
    }

    /// <inheritdoc />
    public async Task<OperationResult<IReadOnlyList<CoberturaFranjaDto>>> CoberturaAsync(DateOnly desde, DateOnly hasta, Guid? cargoId, CancellationToken cancellationToken)
    {
        if (desde > hasta)
        {
            return OperationResult.Failure<IReadOnlyList<CoberturaFranjaDto>>(ErrorType.Validation, RangoInvertido);
        }

        if (hasta.DayNumber - desde.DayNumber + 1 > MaximoDias)
        {
            return OperationResult.Failure<IReadOnlyList<CoberturaFranjaDto>>(ErrorType.Validation, RangoMuyGrande);
        }

        var cargos = (await _cargos.ListarAsync(cancellationToken)).ToList();
        if (cargoId is not null)
        {
            cargos = cargos.Where(c => c.Id == cargoId).ToList();
            if (cargos.Count == 0)
            {
                return OperationResult.Failure<IReadOnlyList<CoberturaFranjaDto>>(ErrorType.NotFound, CargoNoEncontrado);
            }
        }

        var franjasResultado = await _franjas.ListarOperativasAsync(cancellationToken);
        if (franjasResultado.IsFailure)
        {
            return OperationResult.Failure<IReadOnlyList<CoberturaFranjaDto>>(
                franjasResultado.Type ?? ErrorType.Validation,
                franjasResultado.Error ?? "No se pudieron leer las franjas horarias.");
        }

        // Solo las franjas activas: una franja inactiva no se opera, así que no tiene cobertura que medir.
        var franjas = franjasResultado.Value!.Where(f => f.Activa).OrderBy(f => f.HoraInicio).ToList();

        // Solo cuentan los turnos que siguen vigentes: Cancelado, Completado y Ausente no cubren la franja.
        var turnos = (await _turnos.ListarEntreAsync(desde, hasta, cancellationToken))
            .Where(t => t.Estado is EstadoTurno.Programado or EstadoTurno.Confirmado)
            .ToList();

        var filas = new List<CoberturaFranjaDto>();
        for (var fecha = desde; fecha <= hasta; fecha = fecha.AddDays(1))
        {
            foreach (var franja in franjas)
            {
                foreach (var cargo in cargos.OrderBy(c => c.Nombre))
                {
                    // Un turno cubre la franja si empieza antes o a la vez que ella y termina después o a la vez que ella.
                    var planificado = CalculoCobertura.Planificado(turnos, fecha, cargo.Id, franja.HoraInicio, franja.HoraFin);

                    filas.Add(new CoberturaFranjaDto(
                        fecha, franja.Id, franja.Nombre, franja.HoraInicio, franja.HoraFin,
                        cargo.Id, cargo.Nombre, planificado));
                }
            }
        }

        return OperationResult.Success<IReadOnlyList<CoberturaFranjaDto>>(filas);
    }
}
