using GIRA.Application.Common;
using GIRA.Application.DTOs.Configuracion;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Repository.Configuracion;
using GIRA.Application.Interfaces.Services.Configuracion;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Application.Services.Reservas;
using GIRA.Domain.Entities.Configuracion;
using GIRA.Domain.Exceptions.Configuracion;

namespace GIRA.Application.Services.Configuracion;

/// <summary>Implementación de <see cref="IConfiguracionReservasService"/>.</summary>
public class ConfiguracionReservasService : IConfiguracionReservasService
{
    private const string MensajeConcurrencia = "La configuración cambió mientras la editabas; recárgala e inténtalo de nuevo.";

    private readonly IConfiguracionRepository _repositorio;
    private readonly IParametrosReservaService _parametros;
    private readonly IConsultaSalon _salon;
    private readonly TimeProvider _reloj;

    /// <summary>Crea el servicio con el repositorio, los parámetros (para invalidar la caché), el salón y el reloj.</summary>
    public ConfiguracionReservasService(
        IConfiguracionRepository repositorio,
        IParametrosReservaService parametros,
        IConsultaSalon salon,
        TimeProvider reloj)
    {
        _repositorio = repositorio;
        _parametros = parametros;
        _salon = salon;
        _reloj = reloj;
    }

    // --- Políticas ---

    /// <inheritdoc />
    public async Task<OperationResult<ConfiguracionParametrosDto>> ObtenerParametrosAsync(
        bool incluirAuditoria, CancellationToken cancellationToken)
    {
        var guardada = await _repositorio.LeerAsync(cancellationToken);
        return OperationResult.Success(ADto(guardada.Configuracion, incluirAuditoria));
    }

    /// <inheritdoc />
    public async Task<OperationResult<ParametrosGuardadosDto>> ActualizarParametrosAsync(
        ActualizarParametrosRequest request, CancellationToken cancellationToken)
    {
        var configuracion = await _repositorio.BuscarConfiguracionAsync(cancellationToken);
        if (configuracion is null)
        {
            // Primera vez (el seeder no corrió, por ejemplo en producción): se crea la fila única.
            configuracion = new ConfiguracionRestaurante { Id = ConfiguracionRestaurante.IdUnico };
            _repositorio.AgregarConfiguracion(configuracion);
        }

        configuracion.AntelacionMinimaMinutos = request.AntelacionMinimaMinutos;
        configuracion.AntelacionMaximaDias = request.AntelacionMaximaDias;
        configuracion.LimiteCancelacionMinutos = request.LimiteCancelacionMinutos;
        configuracion.VentanaCheckInMinutos = request.VentanaCheckInMinutos;
        configuracion.MaxReservasVigentesPorTelefono = request.MaxReservasVigentesPorTelefono;
        configuracion.MaxPersonasPorReserva = request.MaxPersonasPorReserva;

        if (!await GuardarAsync(cancellationToken))
        {
            return OperationResult.Failure<ParametrosGuardadosDto>(ErrorType.Conflict, MensajeConcurrencia);
        }

        var advertencias = new List<string>();
        var capacidadMaxima = await _salon.CapacidadMaximaMesaActivaAsync(cancellationToken);
        if (capacidadMaxima > 0 && request.MaxPersonasPorReserva > capacidadMaxima)
        {
            advertencias.Add(
                $"El máximo de {request.MaxPersonasPorReserva} personas supera la mesa activa más grande " +
                $"({capacidadMaxima}); el tamaño efectivo de grupo será {capacidadMaxima}.");
        }

        return OperationResult.Success(new ParametrosGuardadosDto(ADto(configuracion, incluirAuditoria: true), advertencias));
    }

    // --- Horario ---

    /// <inheritdoc />
    public async Task<OperationResult<IReadOnlyList<HorarioDiaDto>>> ListarHorarioAsync(CancellationToken cancellationToken)
    {
        var guardada = await _repositorio.LeerAsync(cancellationToken);
        var horarios = ParametrosReservaDbService.Construir(guardada).Horarios;

        IReadOnlyList<HorarioDiaDto> lista = DiasSemanaEs.LunesADomingo
            .Select(dia => ADto(dia, horarios[dia]))
            .ToList();
        return OperationResult.Success(lista);
    }

    /// <inheritdoc />
    public async Task<OperationResult<HorarioGuardadoDto>> ActualizarHorarioAsync(
        DayOfWeek dia, ActualizarHorarioRequest request, CancellationToken cancellationToken)
    {
        if (request.Abierto)
        {
            // Una franja activa debe caber en el horario de todos los días abiertos (GI-226).
            var franjas = await _salon.ListarFranjasActivasAsync(cancellationToken);
            var fuera = franjas
                .Where(f => f.HoraInicio < request.HoraApertura || f.HoraFin > request.HoraCierre)
                .OrderBy(f => f.HoraInicio)
                .FirstOrDefault();
            if (fuera is not null)
            {
                return OperationResult.Failure<HorarioGuardadoDto>(
                    ErrorType.Conflict,
                    $"La franja '{fuera.Nombre}' ({DiasSemanaEs.Hora(fuera.HoraInicio)}–{DiasSemanaEs.Hora(fuera.HoraFin)}) " +
                    $"quedaría fuera del horario del {DiasSemanaEs.Nombre(dia)} " +
                    $"({DiasSemanaEs.Hora(request.HoraApertura)}–{DiasSemanaEs.Hora(request.HoraCierre)}). " +
                    "Ajusta o desactiva la franja primero.");
            }
        }

        var horario = await _repositorio.BuscarHorarioAsync(dia, cancellationToken);
        if (horario is null)
        {
            horario = new HorarioOperacion { Id = Guid.NewGuid(), DiaSemana = dia };
            _repositorio.AgregarHorario(horario);
        }

        horario.Abierto = request.Abierto;
        horario.HoraApertura = request.HoraApertura;
        horario.HoraCierre = request.HoraCierre;

        if (!await GuardarAsync(cancellationToken))
        {
            return OperationResult.Failure<HorarioGuardadoDto>(ErrorType.Conflict, MensajeConcurrencia);
        }

        var advertencias = new List<string>();
        if (!request.Abierto)
        {
            var reservas = await _salon.ContarReservasVigentesPorDiaSemanaAsync(dia, _reloj.HoyLocal(), cancellationToken);
            if (reservas > 0)
            {
                advertencias.Add(
                    $"Hay {Vigentes(reservas)} en próximos {DiasSemanaEs.Nombre(dia)}; " +
                    "no se cancelan, solo se impiden las reservas nuevas ese día.");
            }
        }

        return OperationResult.Success(new HorarioGuardadoDto(ADto(dia, new HorarioDia(horario.Abierto, horario.HoraApertura, horario.HoraCierre)), advertencias));
    }

    // --- Días cerrados ---

    /// <inheritdoc />
    public async Task<OperationResult<IReadOnlyList<DiaCerradoDto>>> ListarDiasCerradosAsync(CancellationToken cancellationToken)
    {
        var dias = await _repositorio.ListarDiasCerradosDesdeAsync(_reloj.HoyLocal(), cancellationToken);
        IReadOnlyList<DiaCerradoDto> lista = dias.Select(d => new DiaCerradoDto(d.Id, d.Fecha, d.Motivo)).ToList();
        return OperationResult.Success(lista);
    }

    /// <inheritdoc />
    public async Task<OperationResult<DiaCerradoCreadoDto>> CrearDiaCerradoAsync(
        CrearDiaCerradoRequest request, CancellationToken cancellationToken)
    {
        if (await _repositorio.ExisteDiaCerradoAsync(request.Fecha, cancellationToken))
        {
            return Duplicado();
        }

        var motivo = string.IsNullOrWhiteSpace(request.Motivo) ? null : request.Motivo.Trim();
        var dia = new DiaCerrado { Id = Guid.NewGuid(), Fecha = request.Fecha, Motivo = motivo };
        _repositorio.AgregarDiaCerrado(dia);

        try
        {
            await GuardarYInvalidarAsync(cancellationToken);
        }
        catch (ConfiguracionDuplicadaException)
        {
            return Duplicado();
        }
        catch (ConfiguracionConflictoConcurrenciaException)
        {
            return OperationResult.Failure<DiaCerradoCreadoDto>(ErrorType.Conflict, MensajeConcurrencia);
        }

        var advertencias = new List<string>();
        var reservas = await _salon.ContarReservasVigentesAsync(request.Fecha, cancellationToken);
        if (reservas > 0)
        {
            advertencias.Add(
                $"Hay {Vigentes(reservas)} ese día; no se cancelan, solo se impiden las reservas nuevas.");
        }

        return OperationResult.Success(
            new DiaCerradoCreadoDto(new DiaCerradoDto(dia.Id, dia.Fecha, dia.Motivo), advertencias));
    }

    /// <inheritdoc />
    public async Task<OperationResult> EliminarDiaCerradoAsync(Guid id, CancellationToken cancellationToken)
    {
        var dia = await _repositorio.BuscarDiaCerradoAsync(id, cancellationToken);
        if (dia is null)
        {
            return OperationResult.Failure(ErrorType.NotFound, "El día cerrado no existe.");
        }

        _repositorio.EliminarDiaCerrado(dia);

        try
        {
            await GuardarYInvalidarAsync(cancellationToken);
        }
        catch (ConfiguracionConflictoConcurrenciaException)
        {
            return OperationResult.Failure(ErrorType.Conflict, MensajeConcurrencia);
        }

        return OperationResult.Success();
    }

    // --- Utilidades ---

    private static OperationResult<DiaCerradoCreadoDto> Duplicado() =>
        OperationResult.Failure<DiaCerradoCreadoDto>(ErrorType.Conflict, "Ya hay un día cerrado en esa fecha.");

    // Guarda e invalida la caché de parámetros. Devuelve false si hubo una modificación concurrente o un
    // choque con algo que otra solicitud acababa de crear (en ambos casos se pide recargar).
    private async Task<bool> GuardarAsync(CancellationToken cancellationToken)
    {
        try
        {
            await GuardarYInvalidarAsync(cancellationToken);
            return true;
        }
        catch (ConfiguracionConflictoConcurrenciaException)
        {
            return false;
        }
        catch (ConfiguracionDuplicadaException)
        {
            return false;
        }
    }

    private async Task GuardarYInvalidarAsync(CancellationToken cancellationToken)
    {
        await _repositorio.GuardarCambiosAsync(cancellationToken);
        _parametros.Invalidar();
    }

    private static string Vigentes(int cantidad) => cantidad == 1 ? "1 reserva vigente" : $"{cantidad} reservas vigentes";

    private static ParametrosEditablesDto PorDefecto() => new(
        ParametrosReservaService.ValoresPorDefecto.AntelacionMinimaMinutos,
        ParametrosReservaService.ValoresPorDefecto.AntelacionMaximaDias,
        ParametrosReservaService.ValoresPorDefecto.LimiteCancelacionMinutos,
        ParametrosReservaService.ValoresPorDefecto.VentanaCheckInMinutos,
        ParametrosReservaService.ValoresPorDefecto.MaxReservasVigentesPorTelefono,
        ParametrosReservaService.ValoresPorDefecto.MaxPersonasPorReserva);

    private static ConfiguracionParametrosDto ADto(ConfiguracionRestaurante? c, bool incluirAuditoria)
    {
        var vigentes = c is null
            ? PorDefecto()
            : new ParametrosEditablesDto(
                c.AntelacionMinimaMinutos, c.AntelacionMaximaDias, c.LimiteCancelacionMinutos,
                c.VentanaCheckInMinutos, c.MaxReservasVigentesPorTelefono, c.MaxPersonasPorReserva);

        return new ConfiguracionParametrosDto(
            vigentes, PorDefecto(), c?.UpdatedAt, incluirAuditoria ? c?.UpdatedBy : null);
    }

    private static HorarioDiaDto ADto(DayOfWeek dia, HorarioDia horario) =>
        new((int)dia, DiasSemanaEs.Nombre(dia), horario.Abierto, horario.HoraApertura, horario.HoraCierre);
}
