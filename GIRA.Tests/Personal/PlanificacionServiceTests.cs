using GIRA.Application.Common;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Application.Interfaces.Services.Reservas;
using GIRA.Application.Services.Personal;
using GIRA.Domain.Entities.Personal;
using GIRA.Domain.Enums.Personal;

namespace GIRA.Tests.Personal;

/// <summary>
/// Pruebas de la planificación semanal con cobertura (GI-301, RF-PER-07). Repositorios y franjas falsos en memoria.
/// Las franjas de prueba son Mañana 08–10, Mediodía 10–12 y Tarde 12–14, más una inactiva 14–16 que no debe contar.
/// </summary>
public class PlanificacionServiceTests
{
    private static readonly DateOnly Lunes = new(2026, 12, 7);
    private static readonly Guid Empleado = Guid.NewGuid();
    private static readonly Guid Mesero = Guid.NewGuid();
    private static readonly Guid Cocina = Guid.NewGuid();

    [Fact]
    public async Task Cobertura_SoloCuentaTurnosQueCubrenLaFranjaCompleta()
    {
        var turnos = new RepositorioTurnosPlanificacion();
        turnos.Turnos.Add(Turno(Lunes, "09:00", "13:00", Mesero));
        var servicio = Crear(turnos);

        var resultado = await servicio.CoberturaAsync(Lunes, Lunes, Mesero, default);

        Assert.True(resultado.IsSuccess);
        var filas = resultado.Value!;
        // 09–13 cubre Mediodía (10–12) completa, pero no Mañana (08–10) ni Tarde (12–14).
        Assert.Equal(1, filas.Single(f => f.FranjaNombre == "Mediodía").PersonalPlanificado);
        Assert.Equal(0, filas.Single(f => f.FranjaNombre == "Mañana").PersonalPlanificado);
        Assert.Equal(0, filas.Single(f => f.FranjaNombre == "Tarde").PersonalPlanificado);
    }

    [Fact]
    public async Task Cobertura_NoCuentaTurnosCanceladosCompletadosNiAusentes()
    {
        var turnos = new RepositorioTurnosPlanificacion();
        turnos.Turnos.Add(Turno(Lunes, "08:00", "10:00", Mesero, EstadoTurno.Cancelado));
        turnos.Turnos.Add(Turno(Lunes, "08:00", "10:00", Mesero, EstadoTurno.Completado));
        turnos.Turnos.Add(Turno(Lunes, "08:00", "10:00", Mesero, EstadoTurno.Ausente));
        turnos.Turnos.Add(Turno(Lunes, "08:00", "10:00", Mesero, EstadoTurno.Confirmado));
        var servicio = Crear(turnos);

        var resultado = await servicio.CoberturaAsync(Lunes, Lunes, Mesero, default);

        // Solo el turno Confirmado cuenta.
        Assert.Equal(1, resultado.Value!.Single(f => f.FranjaNombre == "Mañana").PersonalPlanificado);
    }

    [Fact]
    public async Task Cobertura_NoIncluyeFranjasInactivas()
    {
        var servicio = Crear(new RepositorioTurnosPlanificacion());

        var resultado = await servicio.CoberturaAsync(Lunes, Lunes, null, default);

        // Tres franjas activas por cargo; la inactiva 14–16 no aparece.
        Assert.Equal(3 * 2, resultado.Value!.Count);
        Assert.DoesNotContain(resultado.Value, f => f.FranjaNombre == "Inactiva");
    }

    [Fact]
    public async Task Cobertura_DevuelveLaCuadriculaCompletaConCeros()
    {
        var servicio = Crear(new RepositorioTurnosPlanificacion());

        var resultado = await servicio.CoberturaAsync(Lunes, Lunes.AddDays(1), Cocina, default);

        // Dos días, tres franjas activas, un cargo: seis filas, todas en cero porque no hay turnos.
        Assert.Equal(6, resultado.Value!.Count);
        Assert.All(resultado.Value, f => Assert.Equal(0, f.PersonalPlanificado));
    }

    [Fact]
    public async Task Cobertura_SinCargoId_IncluyeTodosLosCargos()
    {
        var servicio = Crear(new RepositorioTurnosPlanificacion());

        var resultado = await servicio.CoberturaAsync(Lunes, Lunes, null, default);

        Assert.Contains(resultado.Value!, f => f.CargoId == Mesero);
        Assert.Contains(resultado.Value!, f => f.CargoId == Cocina);
    }

    [Fact]
    public async Task Cobertura_ConCargoId_SoloDevuelveEseCargo()
    {
        var servicio = Crear(new RepositorioTurnosPlanificacion());

        var resultado = await servicio.CoberturaAsync(Lunes, Lunes, Cocina, default);

        Assert.All(resultado.Value!, f => Assert.Equal(Cocina, f.CargoId));
    }

    [Fact]
    public async Task Cobertura_CargoInexistente_DevuelveNoEncontrado()
    {
        var servicio = Crear(new RepositorioTurnosPlanificacion());

        var resultado = await servicio.CoberturaAsync(Lunes, Lunes, Guid.NewGuid(), default);

        Assert.Equal(ErrorType.NotFound, resultado.Type);
    }

    [Fact]
    public async Task Cobertura_FechaInicioPosteriorAFin_DevuelveValidacion()
    {
        var servicio = Crear(new RepositorioTurnosPlanificacion());

        var resultado = await servicio.CoberturaAsync(Lunes.AddDays(1), Lunes, null, default);

        Assert.Equal(ErrorType.Validation, resultado.Type);
    }

    [Fact]
    public async Task Cobertura_RangoMayorA31Dias_DevuelveValidacion()
    {
        var servicio = Crear(new RepositorioTurnosPlanificacion());

        // Del lunes al día 32 inclusive son 32 días.
        var resultado = await servicio.CoberturaAsync(Lunes, Lunes.AddDays(31), null, default);

        Assert.Equal(ErrorType.Validation, resultado.Type);
    }

    [Fact]
    public async Task Cobertura_TurnosFueraDelRango_NoCuentan()
    {
        var turnos = new RepositorioTurnosPlanificacion();
        turnos.Turnos.Add(Turno(Lunes.AddDays(-1), "08:00", "10:00", Mesero));
        var servicio = Crear(turnos);

        var resultado = await servicio.CoberturaAsync(Lunes, Lunes, Mesero, default);

        Assert.All(resultado.Value!, f => Assert.Equal(0, f.PersonalPlanificado));
    }

    private static PlanificacionService Crear(RepositorioTurnosPlanificacion turnos) =>
        new(turnos, new RepositorioCargosPlanificacion(), new FranjasFalsas());

    private static Turno Turno(DateOnly fecha, string inicio, string fin, Guid cargo, EstadoTurno estado = EstadoTurno.Programado) =>
        new()
        {
            Id = Guid.NewGuid(),
            EmpleadoId = Empleado,
            CargoId = cargo,
            Fecha = fecha,
            HoraInicio = TimeOnly.Parse(inicio),
            HoraFin = TimeOnly.Parse(fin),
            Estado = estado,
        };

    /// <summary>Repositorio de turnos en memoria con solo los métodos que usa la planificación.</summary>
    private sealed class RepositorioTurnosPlanificacion : ITurnoRepository
    {
        public List<Turno> Turnos { get; } = [];

        public Task<IReadOnlyList<Turno>> ListarEntreAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Turno>>(Turnos.Where(t => t.Fecha >= desde && t.Fecha <= hasta).ToList());

        public Task<Turno?> BuscarAsync(Guid id, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<IReadOnlyList<Turno>> ListarPorEmpleadoYFechaAsync(Guid empleadoId, DateOnly fecha, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<IReadOnlyList<Turno>> ListarPorEmpleadoEntreAsync(Guid empleadoId, DateOnly desde, DateOnly hasta, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<IReadOnlyList<Turno>> ListarFuturosActivosAsync(Guid empleadoId, DateOnly desde, CancellationToken cancellationToken) => throw new NotImplementedException();

        public void Agregar(Turno turno) => Turnos.Add(turno);

        public Task GuardarCambiosAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>Repositorio de cargos con dos cargos de prueba: Mesero y Cocina.</summary>
    private sealed class RepositorioCargosPlanificacion : ICargoRepository
    {
        public Task<IReadOnlyList<Cargo>> ListarAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Cargo>>(
            [
                new Cargo { Id = Mesero, Nombre = "Mesero" },
                new Cargo { Id = Cocina, Nombre = "Cocina" },
            ]);

        public Task<Cargo?> BuscarAsync(Guid id, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken) => throw new NotImplementedException();

        public void Agregar(Cargo cargo) => throw new NotImplementedException();

        public Task GuardarCambiosAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
    }

    /// <summary>Franjas falsas del módulo de Reservas: tres activas y una inactiva. Solo se usa <c>ListarOperativasAsync</c>.</summary>
    private sealed class FranjasFalsas : IFranjaService
    {
        public Task<OperationResult<IReadOnlyList<FranjaOperativaDto>>> ListarOperativasAsync(CancellationToken cancellationToken) =>
            Task.FromResult(OperationResult.Success<IReadOnlyList<FranjaOperativaDto>>(
            [
                new FranjaOperativaDto(Guid.NewGuid(), "Mañana", new TimeOnly(8, 0), new TimeOnly(10, 0), true),
                new FranjaOperativaDto(Guid.NewGuid(), "Mediodía", new TimeOnly(10, 0), new TimeOnly(12, 0), true),
                new FranjaOperativaDto(Guid.NewGuid(), "Tarde", new TimeOnly(12, 0), new TimeOnly(14, 0), true),
                new FranjaOperativaDto(Guid.NewGuid(), "Inactiva", new TimeOnly(14, 0), new TimeOnly(16, 0), false),
            ]));

        public Task<OperationResult<PagedResult<FranjaDto>>> ListarAsync(ListarSalonRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<OperationResult<FranjaDto>> ObtenerAsync(Guid id, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<OperationResult<FranjaDto>> CrearAsync(GuardarFranjaRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<OperationResult<FranjaDto>> ActualizarAsync(Guid id, GuardarFranjaRequest request, CancellationToken cancellationToken) => throw new NotImplementedException();

        public Task<OperationResult<FranjaDto>> CambiarEstadoAsync(Guid id, bool activa, CancellationToken cancellationToken) => throw new NotImplementedException();
    }
}
