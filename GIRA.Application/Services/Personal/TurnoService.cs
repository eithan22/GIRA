using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.Events.Personal;
using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Application.Interfaces.Services.Personal;
using GIRA.Domain.Entities.Personal;
using GIRA.Domain.Enums.Personal;

namespace GIRA.Application.Services.Personal;

/// <summary>
/// Implementación de <see cref="ITurnoService"/>. Aplica RN-PER-01 (disponibilidad), RN-PER-02 (sin solapamiento),
/// RN-PER-03 (sin ausencia aprobada), RN-PER-05 (descanso mínimo entre turnos, GI-306) y RN-PER-10 (los turnos pasados no se modifican).
/// </summary>
public class TurnoService : ITurnoService
{
    /// <summary>
    /// Descanso mínimo por defecto entre el fin de un turno y el inicio del siguiente del mismo empleado.
    /// Valor provisional tomado de la Directiva 2003/88/CE (11 horas); el equipo debe confirmar la cifra
    /// que aplica al restaurante. Se puede cambiar pasando otro valor al constructor.
    /// </summary>
    public static readonly TimeSpan DescansoMinimoPorDefecto = TimeSpan.FromHours(11);

    private const string EmpleadoNoEncontrado = "El empleado no existe.";
    private const string EmpleadoInactivo = "El empleado está inactivo y no se le pueden asignar turnos.";
    private const string CargoInexistente = "El cargo indicado no existe.";
    private const string FueraDeDisponibilidad = "El horario queda fuera de la disponibilidad del empleado para ese día.";
    private const string Solapado = "El horario se solapa con otro turno del mismo empleado.";
    private const string DescansoInsuficiente = "No se respeta el descanso mínimo entre turnos del empleado.";
    private const string AusenciaAprobada = "El empleado tiene una ausencia aprobada en esa fecha.";
    private const string TurnoNoNoModificable = "Un turno pasado o cancelado no se puede modificar.";
    private const string NoEncontrado = "El turno no existe.";
    private const string SuperaHoras = "El turno supera las horas máximas de la semana del empleado.";
    private const string EstadoNoPermitido = "Solo se puede cambiar el estado a Cancelado, Completado o Ausente.";
    private const string TurnoCancelado = "Un turno cancelado no se puede marcar como completado o ausente.";
    private const string SoloTurnosPasados = "Solo se marcan como completados o ausentes los turnos que ya pasaron.";

    private readonly ITurnoRepository _turnos;
    private readonly IAusenciaRepository _ausencias;
    private readonly IEmpleadoRepository _empleados;
    private readonly ICargoRepository _cargos;
    private readonly IDisponibilidadRepository _disponibilidades;
    private readonly IValidator<TurnoRequest> _validador;
    private readonly TimeProvider _reloj;
    private readonly IPublicadorEventosSeguro _publicador;
    private readonly TimeSpan _descansoMinimo;

    /// <summary>
    /// Crea el servicio con sus repositorios, el validador, el reloj del restaurante, el publicador de eventos
    /// y, opcionalmente, el descanso mínimo entre turnos (por defecto <see cref="DescansoMinimoPorDefecto"/>).
    /// </summary>
    public TurnoService(
        ITurnoRepository turnos,
        IAusenciaRepository ausencias,
        IEmpleadoRepository empleados,
        ICargoRepository cargos,
        IDisponibilidadRepository disponibilidades,
        IValidator<TurnoRequest> validador,
        TimeProvider reloj,
        IPublicadorEventosSeguro publicador,
        TimeSpan? descansoMinimo = null)
    {
        _turnos = turnos;
        _ausencias = ausencias;
        _empleados = empleados;
        _cargos = cargos;
        _disponibilidades = disponibilidades;
        _validador = validador;
        _reloj = reloj;
        _publicador = publicador;
        _descansoMinimo = descansoMinimo ?? DescansoMinimoPorDefecto;
    }

    /// <inheritdoc />
    public async Task<OperationResult<TurnoDto>> CrearAsync(TurnoRequest request, CancellationToken cancellationToken)
    {
        var validacion = await ValidarFormatoAsync(request, cancellationToken);
        if (validacion is not null)
        {
            return validacion;
        }

        var error = await RevisarReglasAsync(request, null, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        var turno = new Turno();
        Copiar(request, turno);
        _turnos.Agregar(turno);
        await _turnos.GuardarCambiosAsync(cancellationToken);
        await _publicador.PublicarAsync(
            new TurnoAsignadoEvent(turno.Id, turno.EmpleadoId, turno.Fecha, turno.HoraInicio, turno.HoraFin),
            $"TurnoId={turno.Id}",
            cancellationToken);

        return OperationResult.Success(ToDto(turno));
    }

    /// <inheritdoc />
    public async Task<OperationResult<TurnoDto>> ActualizarAsync(Guid id, TurnoRequest request, CancellationToken cancellationToken)
    {
        var validacion = await ValidarFormatoAsync(request, cancellationToken);
        if (validacion is not null)
        {
            return validacion;
        }

        var turno = await _turnos.BuscarAsync(id, cancellationToken);
        if (turno is null)
        {
            return OperationResult.Failure<TurnoDto>(ErrorType.NotFound, NoEncontrado);
        }

        if (!EsModificable(turno))
        {
            return OperationResult.Failure<TurnoDto>(ErrorType.Conflict, TurnoNoNoModificable);
        }

        var error = await RevisarReglasAsync(request, id, cancellationToken);
        if (error is not null)
        {
            return error;
        }

        Copiar(request, turno);
        await _turnos.GuardarCambiosAsync(cancellationToken);
        await _publicador.PublicarAsync(
            new TurnoModificadoEvent(turno.Id, turno.EmpleadoId, "Horario o datos modificados"),
            $"TurnoId={turno.Id}",
            cancellationToken);

        return OperationResult.Success(ToDto(turno));
    }

    /// <inheritdoc />
    public async Task<OperationResult<TurnoDto>> CambiarEstadoAsync(Guid id, EstadoTurno estado, CancellationToken cancellationToken)
    {
        if (estado is not (EstadoTurno.Cancelado or EstadoTurno.Completado or EstadoTurno.Ausente))
        {
            return OperationResult.Failure<TurnoDto>(ErrorType.Validation, EstadoNoPermitido);
        }

        var turno = await _turnos.BuscarAsync(id, cancellationToken);
        if (turno is null)
        {
            return OperationResult.Failure<TurnoDto>(ErrorType.NotFound, NoEncontrado);
        }

        if (estado == EstadoTurno.Cancelado)
        {
            if (!EsModificable(turno))
            {
                return OperationResult.Failure<TurnoDto>(ErrorType.Conflict, TurnoNoNoModificable);
            }
        }
        else
        {
            // GI-307 y RN-PER-10: solo los turnos que ya pasaron se marcan como Completado o Ausente.
            if (turno.Estado == EstadoTurno.Cancelado)
            {
                return OperationResult.Failure<TurnoDto>(ErrorType.Conflict, TurnoCancelado);
            }

            if (turno.Fecha >= _reloj.HoyLocal())
            {
                return OperationResult.Failure<TurnoDto>(ErrorType.Conflict, SoloTurnosPasados);
            }
        }

        turno.Estado = estado;
        await _turnos.GuardarCambiosAsync(cancellationToken);
        await _publicador.PublicarAsync(
            new TurnoModificadoEvent(turno.Id, turno.EmpleadoId, $"Estado: {estado}"),
            $"TurnoId={turno.Id}",
            cancellationToken);

        return OperationResult.Success(ToDto(turno));
    }

    /// <summary>Un turno se puede modificar si no está cancelado y su fecha no ha pasado (RN-PER-10).</summary>
    private bool EsModificable(Turno turno) =>
        turno.Estado != EstadoTurno.Cancelado && turno.Fecha >= _reloj.HoyLocal();

    private async Task<OperationResult<TurnoDto>?> ValidarFormatoAsync(TurnoRequest request, CancellationToken cancellationToken)
    {
        var resultado = await _validador.ValidateAsync(request, cancellationToken);
        if (resultado.IsValid)
        {
            return null;
        }

        var mensaje = string.Join(" ", resultado.Errors.Select(e => e.ErrorMessage));
        return OperationResult.Failure<TurnoDto>(ErrorType.Validation, mensaje);
    }

    /// <summary>Revisa empleado, cargo, disponibilidad, ausencias y solapamiento. Devuelve null si todo está bien.</summary>
    private async Task<OperationResult<TurnoDto>?> RevisarReglasAsync(TurnoRequest request, Guid? excluirId, CancellationToken cancellationToken)
    {
        var empleado = await _empleados.BuscarAsync(request.EmpleadoId, cancellationToken);
        if (empleado is null)
        {
            return OperationResult.Failure<TurnoDto>(ErrorType.NotFound, EmpleadoNoEncontrado);
        }

        if (!empleado.Activo)
        {
            return OperationResult.Failure<TurnoDto>(ErrorType.Validation, EmpleadoInactivo);
        }

        if (await _cargos.BuscarAsync(request.CargoId, cancellationToken) is null)
        {
            return OperationResult.Failure<TurnoDto>(ErrorType.Validation, CargoInexistente);
        }

        // RN-PER-01: el turno debe caber dentro de un bloque de disponibilidad de ese día de la semana.
        var disponibilidad = await _disponibilidades.ListarPorEmpleadoAsync(request.EmpleadoId, cancellationToken);
        var cabe = disponibilidad.Any(d =>
            d.DiaSemana == request.Fecha.DayOfWeek &&
            d.HoraInicio <= request.HoraInicio &&
            request.HoraFin <= d.HoraFin);
        if (!cabe)
        {
            return OperationResult.Failure<TurnoDto>(ErrorType.Validation, FueraDeDisponibilidad);
        }

        // RN-PER-03: no se asigna en fechas con una ausencia aprobada.
        if (await _ausencias.TieneAprobadaEnFechaAsync(request.EmpleadoId, request.Fecha, cancellationToken))
        {
            return OperationResult.Failure<TurnoDto>(ErrorType.Conflict, AusenciaAprobada);
        }

        // RN-PER-02: no puede haber turnos que se solapen para el mismo empleado.
        var delDia = await _turnos.ListarPorEmpleadoYFechaAsync(request.EmpleadoId, request.Fecha, cancellationToken);
        var solapa = delDia.Any(t =>
            t.Id != excluirId &&
            t.Estado != EstadoTurno.Cancelado &&
            request.HoraInicio < t.HoraFin &&
            t.HoraInicio < request.HoraFin);
        if (solapa)
        {
            return OperationResult.Failure<TurnoDto>(ErrorType.Conflict, Solapado);
        }

        // RN-PER-05 (GI-306): debe haber un descanso mínimo entre el fin de un turno y el inicio del siguiente.
        // Se revisan los turnos del día anterior, del mismo día y del día siguiente, porque el descanso puede cruzar la medianoche.
        var cercanos = await _turnos.ListarPorEmpleadoEntreAsync(request.EmpleadoId, request.Fecha.AddDays(-1), request.Fecha.AddDays(1), cancellationToken);
        var inicioNuevo = request.Fecha.ToDateTime(request.HoraInicio);
        var finNuevo = request.Fecha.ToDateTime(request.HoraFin);
        var descansoInsuficiente = cercanos.Any(t =>
            t.Id != excluirId &&
            t.Estado != EstadoTurno.Cancelado &&
            ((t.Fecha.ToDateTime(t.HoraFin) <= inicioNuevo && inicioNuevo - t.Fecha.ToDateTime(t.HoraFin) < _descansoMinimo) ||
             (finNuevo <= t.Fecha.ToDateTime(t.HoraInicio) && t.Fecha.ToDateTime(t.HoraInicio) - finNuevo < _descansoMinimo)));
        if (descansoInsuficiente)
        {
            var mensaje = $"{DescansoInsuficiente} Descanso mínimo configurado: {_descansoMinimo.TotalHours:0.##} horas.";
            return OperationResult.Failure<TurnoDto>(ErrorType.Validation, mensaje);
        }

        // RN-PER-04 (GI-299): las horas de la semana no pueden superar HorasMaximasSemana.
        // Semana de lunes a domingo; el SRS no dice qué día empieza la semana (pendiente con el equipo).
        var lunes = request.Fecha.AddDays(-(((int)request.Fecha.DayOfWeek + 6) % 7));
        var domingo = lunes.AddDays(6);
        var semana = await _turnos.ListarPorEmpleadoEntreAsync(request.EmpleadoId, lunes, domingo, cancellationToken);
        var acumuladas = semana
            .Where(t => t.Id != excluirId && t.Estado != EstadoTurno.Cancelado)
            .Sum(t => (t.HoraFin - t.HoraInicio).TotalHours);
        var nuevas = (request.HoraFin - request.HoraInicio).TotalHours;
        if (acumuladas + nuevas > empleado.HorasMaximasSemana)
        {
            var mensaje = $"{SuperaHoras} Horas acumuladas en la semana: {acumuladas:0.##} de {empleado.HorasMaximasSemana}; este turno suma {nuevas:0.##}.";
            return OperationResult.Failure<TurnoDto>(ErrorType.Validation, mensaje);
        }

        return null;
    }

    private static void Copiar(TurnoRequest request, Turno turno)
    {
        turno.EmpleadoId = request.EmpleadoId;
        turno.CargoId = request.CargoId;
        turno.Fecha = request.Fecha;
        turno.HoraInicio = request.HoraInicio;
        turno.HoraFin = request.HoraFin;
        turno.Notas = string.IsNullOrWhiteSpace(request.Notas) ? null : request.Notas.Trim();
    }

    /// <summary>Mapeo manual de la entidad al DTO.</summary>
    private static TurnoDto ToDto(Turno t) =>
        new(t.Id, t.EmpleadoId, t.CargoId, t.Fecha, t.HoraInicio, t.HoraFin, t.Estado, t.Notas);
}
