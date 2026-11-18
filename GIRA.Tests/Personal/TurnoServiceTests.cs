using FluentValidation;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Application.Services.Personal;
using GIRA.Application.Validations.Personal;
using GIRA.Domain.Entities.Personal;
using GIRA.Domain.Enums.Personal;

namespace GIRA.Tests.Personal;

/// <summary>
/// Pruebas del servicio de turnos (GI-331): disponibilidad (RN-PER-01), solapamiento (RN-PER-02),
/// ausencias aprobadas (RN-PER-03), turnos pasados (RN-PER-10) y cancelación. Repositorios en memoria
/// y reloj fijo para que las fechas no dependan del día en que se corren las pruebas.
/// </summary>
public class TurnoServiceTests
{
    // 7 de diciembre de 2026 es lunes; el reloj fijo es 18 de noviembre de 2026.
    private static readonly DateOnly Lunes = new(2026, 12, 7);
    private static readonly Guid Empleado = Guid.NewGuid();
    private static readonly Guid Cargo = Guid.NewGuid();

    [Fact]
    public async Task Crear_DentroDeDisponibilidad_SeAsigna()
    {
        var turnos = new RepositorioTurnosEnMemoria();
        var servicio = Crear(turnos);

        var resultado = await servicio.CrearAsync(Solicitud(Lunes, "09:00", "13:00"), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(EstadoTurno.Programado, resultado.Value!.Estado);
        Assert.Single(turnos.Turnos);
    }

    [Fact]
    public async Task Crear_FueraDeDisponibilidad_DevuelveValidacion()
    {
        var servicio = Crear(new RepositorioTurnosEnMemoria());

        var resultado = await servicio.CrearAsync(Solicitud(Lunes, "19:00", "21:00"), default);

        Assert.Equal(ErrorType.Validation, resultado.Type);
    }

    [Fact]
    public async Task Crear_DiaSinDisponibilidad_DevuelveValidacion()
    {
        var servicio = Crear(new RepositorioTurnosEnMemoria());

        // Martes: no hay disponibilidad ese día.
        var resultado = await servicio.CrearAsync(Solicitud(Lunes.AddDays(1), "09:00", "13:00"), default);

        Assert.Equal(ErrorType.Validation, resultado.Type);
    }

    [Fact]
    public async Task Crear_SolapadoConOtroTurno_DevuelveConflicto()
    {
        var turnos = new RepositorioTurnosEnMemoria();
        turnos.Turnos.Add(Turno(Lunes, "09:00", "13:00"));
        var servicio = Crear(turnos);

        var resultado = await servicio.CrearAsync(Solicitud(Lunes, "12:00", "15:00"), default);

        Assert.Equal(ErrorType.Conflict, resultado.Type);
        Assert.Single(turnos.Turnos);
    }

    [Fact]
    public async Task Crear_TurnosContiguos_NoSeSolapan()
    {
        var turnos = new RepositorioTurnosEnMemoria();
        turnos.Turnos.Add(Turno(Lunes, "09:00", "13:00"));
        var servicio = Crear(turnos);

        var resultado = await servicio.CrearAsync(Solicitud(Lunes, "13:00", "17:00"), default);

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public async Task Crear_ConAusenciaAprobada_DevuelveConflicto()
    {
        var ausencias = new RepositorioAusenciasFalso(aprobadaEn: Lunes);
        var servicio = Crear(new RepositorioTurnosEnMemoria(), ausencias);

        var resultado = await servicio.CrearAsync(Solicitud(Lunes, "09:00", "13:00"), default);

        Assert.Equal(ErrorType.Conflict, resultado.Type);
    }

    [Fact]
    public async Task Crear_EmpleadoInactivo_DevuelveValidacion()
    {
        var servicio = Crear(new RepositorioTurnosEnMemoria(), empleadoActivo: false);

        var resultado = await servicio.CrearAsync(Solicitud(Lunes, "09:00", "13:00"), default);

        Assert.Equal(ErrorType.Validation, resultado.Type);
    }

    [Fact]
    public async Task Actualizar_TurnoPasado_NoSeModifica()
    {
        var turnos = new RepositorioTurnosEnMemoria();
        var pasado = Turno(new DateOnly(2026, 11, 10), "09:00", "13:00");
        turnos.Turnos.Add(pasado);
        var servicio = Crear(turnos);

        var resultado = await servicio.ActualizarAsync(pasado.Id, Solicitud(new DateOnly(2026, 11, 10), "10:00", "14:00"), default);

        Assert.Equal(ErrorType.Conflict, resultado.Type);
    }

    [Fact]
    public async Task Actualizar_ConservarElPropioHorario_NoEsConflicto()
    {
        var turnos = new RepositorioTurnosEnMemoria();
        var turno = Turno(Lunes, "09:00", "13:00");
        turnos.Turnos.Add(turno);
        var servicio = Crear(turnos);

        var resultado = await servicio.ActualizarAsync(turno.Id, Solicitud(Lunes, "09:00", "13:00", notas: "Cubre barra"), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("Cubre barra", turnos.Turnos.Single().Notas);
    }

    [Fact]
    public async Task Crear_SuperaLasHorasDeLaSemana_DevuelveValidacion()
    {
        var turnos = new RepositorioTurnosEnMemoria();
        // Lunes, miércoles y jueves de 12 horas cada uno: 36 horas acumuladas en la semana.
        turnos.Turnos.Add(Turno(Lunes, "06:00", "18:00"));
        turnos.Turnos.Add(Turno(Lunes.AddDays(2), "06:00", "18:00"));
        turnos.Turnos.Add(Turno(Lunes.AddDays(3), "06:00", "18:00"));
        var servicio = Crear(turnos);

        // Viernes: 5 horas más sumarían 41, por encima de 40.
        var resultado = await servicio.CrearAsync(Solicitud(Lunes.AddDays(4), "09:00", "14:00"), default);

        Assert.Equal(ErrorType.Validation, resultado.Type);
        Assert.Contains("Horas acumuladas en la semana: 36 de 40", resultado.Error);
    }

    [Fact]
    public async Task Crear_LlegaJustoAlLimite_SeAsigna()
    {
        var turnos = new RepositorioTurnosEnMemoria();
        turnos.Turnos.Add(Turno(Lunes, "06:00", "18:00"));
        turnos.Turnos.Add(Turno(Lunes.AddDays(2), "06:00", "18:00"));
        turnos.Turnos.Add(Turno(Lunes.AddDays(3), "06:00", "18:00"));
        var servicio = Crear(turnos);

        // 36 horas acumuladas; 4 más llegan justo a 40.
        var resultado = await servicio.CrearAsync(Solicitud(Lunes.AddDays(4), "06:00", "10:00"), default);

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public async Task Crear_TurnosCancelados_NoCuentanParaLasHoras()
    {
        var turnos = new RepositorioTurnosEnMemoria();
        // Tres turnos cancelados de 12 horas: si contaran, el nuevo superaría 40.
        foreach (var dia in new[] { Lunes, Lunes.AddDays(2), Lunes.AddDays(3) })
        {
            var cancelado = Turno(dia, "06:00", "18:00");
            cancelado.Estado = EstadoTurno.Cancelado;
            turnos.Turnos.Add(cancelado);
        }
        var servicio = Crear(turnos);

        var resultado = await servicio.CrearAsync(Solicitud(Lunes.AddDays(4), "06:00", "12:00"), default);

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public async Task Cancelar_TurnoVigente_LoMarcaCancelado()
    {
        var turnos = new RepositorioTurnosEnMemoria();
        var turno = Turno(Lunes, "09:00", "13:00");
        turnos.Turnos.Add(turno);
        var servicio = Crear(turnos);

        var resultado = await servicio.CambiarEstadoAsync(turno.Id, EstadoTurno.Cancelado, default);

        Assert.Equal(EstadoTurno.Cancelado, resultado.Value!.Estado);
    }

    [Fact]
    public async Task CambiarEstado_CompletadoDeTurnoFuturo_DevuelveConflicto()
    {
        var turnos = new RepositorioTurnosEnMemoria();
        var turno = Turno(Lunes, "09:00", "13:00");
        turnos.Turnos.Add(turno);
        var servicio = Crear(turnos);

        var resultado = await servicio.CambiarEstadoAsync(turno.Id, EstadoTurno.Completado, default);

        Assert.Equal(ErrorType.Conflict, resultado.Type);
        Assert.Equal(EstadoTurno.Programado, turnos.Turnos.Single().Estado);
    }

    [Fact]
    public async Task CambiarEstado_CompletadoYAusente_EnTurnoPasado_SeGuarda()
    {
        var turnos = new RepositorioTurnosEnMemoria();
        var completado = Turno(new DateOnly(2026, 11, 10), "09:00", "13:00");
        var ausente = Turno(new DateOnly(2026, 11, 11), "09:00", "13:00");
        turnos.Turnos.Add(completado);
        turnos.Turnos.Add(ausente);
        var servicio = Crear(turnos);

        var a = await servicio.CambiarEstadoAsync(completado.Id, EstadoTurno.Completado, default);
        var b = await servicio.CambiarEstadoAsync(ausente.Id, EstadoTurno.Ausente, default);

        Assert.Equal(EstadoTurno.Completado, a.Value!.Estado);
        Assert.Equal(EstadoTurno.Ausente, b.Value!.Estado);
    }

    [Fact]
    public async Task CambiarEstado_TurnoCancelado_NoSePuedeMarcarComoCompletado()
    {
        var turnos = new RepositorioTurnosEnMemoria();
        var turno = Turno(new DateOnly(2026, 11, 10), "09:00", "13:00");
        turno.Estado = EstadoTurno.Cancelado;
        turnos.Turnos.Add(turno);
        var servicio = Crear(turnos);

        var resultado = await servicio.CambiarEstadoAsync(turno.Id, EstadoTurno.Completado, default);

        Assert.Equal(ErrorType.Conflict, resultado.Type);
    }

    [Fact]
    public async Task CambiarEstado_EstadoProgramado_NoSeAcepta()
    {
        var turnos = new RepositorioTurnosEnMemoria();
        var turno = Turno(Lunes, "09:00", "13:00");
        turnos.Turnos.Add(turno);
        var servicio = Crear(turnos);

        var resultado = await servicio.CambiarEstadoAsync(turno.Id, EstadoTurno.Programado, default);

        Assert.Equal(ErrorType.Validation, resultado.Type);
    }

    private static TurnoRequest Solicitud(DateOnly fecha, string inicio, string fin, string? notas = null) =>
        new(Empleado, Cargo, fecha, TimeOnly.Parse(inicio), TimeOnly.Parse(fin), notas);

    private static Turno Turno(DateOnly fecha, string inicio, string fin) =>
        new()
        {
            Id = Guid.NewGuid(),
            EmpleadoId = Empleado,
            CargoId = Cargo,
            Fecha = fecha,
            HoraInicio = TimeOnly.Parse(inicio),
            HoraFin = TimeOnly.Parse(fin),
            Estado = EstadoTurno.Programado,
        };

    private static TurnoService Crear(RepositorioTurnosEnMemoria turnos, RepositorioAusenciasFalso? ausencias = null, bool empleadoActivo = true) =>
        new(
            turnos,
            ausencias ?? new RepositorioAusenciasFalso(),
            new RepositorioEmpleadosFalso(empleadoActivo),
            new RepositorioCargosFalso(),
            new RepositorioDisponibilidadFalso(),
            new TurnoRequestValidator(),
            new RelojFijo(new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero)));

    /// <summary>Reloj con una hora fija, para que RN-PER-10 se pruebe siempre igual.</summary>
    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    /// <summary>Disponibilidad falsa: de 06:00 a 18:00 todos los días menos el martes, para cualquier empleado.</summary>
    private sealed class RepositorioDisponibilidadFalso : IDisponibilidadRepository
    {
        public Task<IReadOnlyList<Disponibilidad>> ListarPorEmpleadoAsync(Guid empleadoId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Disponibilidad>>(
                Enum.GetValues<DayOfWeek>()
                    .Where(d => d != DayOfWeek.Tuesday)
                    .Select(d => new Disponibilidad { EmpleadoId = empleadoId, DiaSemana = d, HoraInicio = new TimeOnly(6, 0), HoraFin = new TimeOnly(18, 0) })
                    .ToList());

        public Task<Disponibilidad?> BuscarAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<Disponibilidad?>(null);
        public void Agregar(Disponibilidad disponibilidad) { }
        public void Eliminar(Disponibilidad disponibilidad) { }
        public Task GuardarCambiosAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RepositorioEmpleadosFalso(bool activo) : IEmpleadoRepository
    {
        public Task<IReadOnlyList<Empleado>> ListarAsync(bool soloActivos, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Empleado>>([]);

        public Task<Empleado?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<Empleado?>(new Empleado { Id = id, Activo = activo, HorasMaximasSemana = 40 });

        public Task<bool> ExisteCedulaAsync(string cedula, Guid? excluirId, CancellationToken cancellationToken) => Task.FromResult(false);
        public void Agregar(Empleado empleado) { }
        public Task GuardarCambiosAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RepositorioCargosFalso : ICargoRepository
    {
        public Task<IReadOnlyList<Cargo>> ListarAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Cargo>>([]);
        public Task<Cargo?> BuscarAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult<Cargo?>(new Cargo { Id = id, Nombre = "Mesero" });
        public Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken) => Task.FromResult(false);
        public void Agregar(Cargo cargo) { }
        public Task GuardarCambiosAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class RepositorioAusenciasFalso(DateOnly? aprobadaEn = null) : IAusenciaRepository
    {
        public Task<bool> TieneAprobadaEnFechaAsync(Guid empleadoId, DateOnly fecha, CancellationToken cancellationToken) =>
            Task.FromResult(aprobadaEn == fecha);
    }

    /// <summary>Repositorio de turnos en memoria.</summary>
    private sealed class RepositorioTurnosEnMemoria : ITurnoRepository
    {
        public List<Turno> Turnos { get; } = [];

        public Task<Turno?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Turnos.FirstOrDefault(t => t.Id == id));

        public Task<IReadOnlyList<Turno>> ListarPorEmpleadoYFechaAsync(Guid empleadoId, DateOnly fecha, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Turno>>(Turnos.Where(t => t.EmpleadoId == empleadoId && t.Fecha == fecha).ToList());

        public Task<IReadOnlyList<Turno>> ListarPorEmpleadoEntreAsync(Guid empleadoId, DateOnly desde, DateOnly hasta, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Turno>>(Turnos.Where(t => t.EmpleadoId == empleadoId && t.Fecha >= desde && t.Fecha <= hasta).ToList());

        public void Agregar(Turno turno)
        {
            turno.Id = Guid.NewGuid();
            Turnos.Add(turno);
        }

        public Task GuardarCambiosAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
