using GIRA.Application.Common;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Application.Services.Personal;
using GIRA.Application.Validations.Personal;
using GIRA.Domain.Entities.Personal;
using GIRA.Domain.Enums.Personal;
using GIRA.Domain.Exceptions.Personal;

namespace GIRA.Tests.Personal;

/// <summary>
/// Pruebas del servicio de empleados (GI-317): alta, cédula única, cargo existente, edición y activar o
/// desactivar. Usan repositorios en memoria para no depender de la base de datos.
/// </summary>
public class EmpleadoServiceTests
{
    private static readonly DateOnly Ingreso = new(2026, 1, 15);

    [Fact]
    public async Task Crear_DatosValidos_RegistraEmpleadoActivo()
    {
        var cargoId = Guid.NewGuid();
        var empleados = new RepositorioEmpleadosEnMemoria();
        var servicio = CrearServicio(empleados, new RepositorioCargosEnMemoria(cargoId));

        var resultado = await servicio.CrearAsync(Datos(cargoId, cedula: " 001-0000001-1 "), default);

        Assert.True(resultado.IsSuccess);
        Assert.True(resultado.Value!.Activo);
        Assert.Equal("001-0000001-1", resultado.Value.Cedula);
        Assert.Single(empleados.Empleados);
    }

    [Fact]
    public async Task Crear_CedulaRepetida_DevuelveConflictoYNoGuarda()
    {
        var cargoId = Guid.NewGuid();
        var empleados = new RepositorioEmpleadosEnMemoria();
        empleados.Empleados.Add(new Empleado { Id = Guid.NewGuid(), Cedula = "001-0000001-1", CargoId = cargoId, Activo = true });
        var servicio = CrearServicio(empleados, new RepositorioCargosEnMemoria(cargoId));

        var resultado = await servicio.CrearAsync(Datos(cargoId, cedula: "001-0000001-1"), default);

        Assert.Equal(ErrorType.Conflict, resultado.Type);
        Assert.Single(empleados.Empleados);
    }

    [Fact]
    public async Task Crear_CargoInexistente_DevuelveValidacion()
    {
        var servicio = CrearServicio(new RepositorioEmpleadosEnMemoria(), new RepositorioCargosEnMemoria(Guid.NewGuid()));

        var resultado = await servicio.CrearAsync(Datos(Guid.NewGuid()), default);

        Assert.Equal(ErrorType.Validation, resultado.Type);
    }

    [Fact]
    public async Task Crear_HorasFueraDeRango_DevuelveValidacion()
    {
        var cargoId = Guid.NewGuid();
        var servicio = CrearServicio(new RepositorioEmpleadosEnMemoria(), new RepositorioCargosEnMemoria(cargoId));

        var resultado = await servicio.CrearAsync(Datos(cargoId) with { HorasMaximasSemana = 0 }, default);

        Assert.Equal(ErrorType.Validation, resultado.Type);
    }

    [Fact]
    public async Task Crear_ChoqueConElIndiceUnico_DevuelveConflicto()
    {
        var cargoId = Guid.NewGuid();
        var empleados = new RepositorioEmpleadosEnMemoria { FallarAlGuardar = true };
        var servicio = CrearServicio(empleados, new RepositorioCargosEnMemoria(cargoId));

        var resultado = await servicio.CrearAsync(Datos(cargoId), default);

        Assert.Equal(ErrorType.Conflict, resultado.Type);
    }

    [Fact]
    public async Task Actualizar_EmpleadoInexistente_DevuelveNoEncontrado()
    {
        var cargoId = Guid.NewGuid();
        var servicio = CrearServicio(new RepositorioEmpleadosEnMemoria(), new RepositorioCargosEnMemoria(cargoId));

        var resultado = await servicio.ActualizarAsync(Guid.NewGuid(), Datos(cargoId), default);

        Assert.Equal(ErrorType.NotFound, resultado.Type);
    }

    [Fact]
    public async Task Actualizar_ConservarLaPropiaCedula_NoEsConflicto()
    {
        var cargoId = Guid.NewGuid();
        var id = Guid.NewGuid();
        var empleados = new RepositorioEmpleadosEnMemoria();
        empleados.Empleados.Add(new Empleado { Id = id, Cedula = "001-0000001-1", CargoId = cargoId, Activo = true });
        var servicio = CrearServicio(empleados, new RepositorioCargosEnMemoria(cargoId));

        var resultado = await servicio.ActualizarAsync(id, Datos(cargoId, cedula: "001-0000001-1") with { Telefono = "809-555-0101" }, default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("809-555-0101", empleados.Empleados.Single().Telefono);
    }

    [Fact]
    public async Task Actualizar_CedulaDeOtroEmpleado_DevuelveConflicto()
    {
        var cargoId = Guid.NewGuid();
        var id = Guid.NewGuid();
        var empleados = new RepositorioEmpleadosEnMemoria();
        empleados.Empleados.Add(new Empleado { Id = id, Cedula = "001-0000001-1", CargoId = cargoId, Activo = true });
        empleados.Empleados.Add(new Empleado { Id = Guid.NewGuid(), Cedula = "002-0000002-2", CargoId = cargoId, Activo = true });
        var servicio = CrearServicio(empleados, new RepositorioCargosEnMemoria(cargoId));

        var resultado = await servicio.ActualizarAsync(id, Datos(cargoId, cedula: "002-0000002-2"), default);

        Assert.Equal(ErrorType.Conflict, resultado.Type);
        Assert.Equal("001-0000001-1", empleados.Empleados.Single(e => e.Id == id).Cedula);
    }

    [Fact]
    public async Task CambiarEstado_Desactivar_NoEliminaYListarSoloActivosLoExcluye()
    {
        var cargoId = Guid.NewGuid();
        var id = Guid.NewGuid();
        var empleados = new RepositorioEmpleadosEnMemoria();
        empleados.Empleados.Add(new Empleado { Id = id, Nombre = "Ana", Apellido = "Pérez", Cedula = "001", CargoId = cargoId, Activo = true });
        var servicio = CrearServicio(empleados, new RepositorioCargosEnMemoria(cargoId));

        var desactivado = await servicio.CambiarEstadoAsync(id, false, default);
        var activos = await servicio.ListarAsync(soloActivos: true, default);
        var todos = await servicio.ListarAsync(soloActivos: false, default);

        Assert.False(desactivado.Value!.Activo);
        Assert.Empty(activos.Value!);
        Assert.Single(todos.Value!);
    }

    private static EmpleadoRequest Datos(Guid cargoId, string cedula = "001-0000001-1") =>
        new("Ana", "Pérez", cedula, null, null, cargoId, Ingreso, 40);

    private static EmpleadoService CrearServicio(
        RepositorioEmpleadosEnMemoria empleados,
        RepositorioCargosEnMemoria cargos,
        RepositorioTurnosFalso? turnos = null,
        PublicadorFalso? publicador = null) =>
        new(
            empleados,
            cargos,
            turnos ?? new RepositorioTurnosFalso(),
            publicador ?? new PublicadorFalso(),
            new EmpleadoRequestValidator(),
            new RelojFijo(new DateTimeOffset(2026, 11, 18, 12, 0, 0, TimeSpan.Zero)));

    [Fact]
    public async Task Desactivar_CancelaSoloLosTurnosFuturosActivosYPublicaCadaCambio()
    {
        var cargoId = Guid.NewGuid();
        var id = Guid.NewGuid();
        var empleados = new RepositorioEmpleadosEnMemoria();
        empleados.Empleados.Add(new Empleado { Id = id, Nombre = "Ana", Apellido = "Pérez", Cedula = "001", CargoId = cargoId, Activo = true });
        var turnos = new RepositorioTurnosFalso();
        var futuro = Turno(id, new DateOnly(2026, 12, 7), EstadoTurno.Programado);
        var pasado = Turno(id, new DateOnly(2026, 11, 10), EstadoTurno.Programado);
        turnos.Turnos.Add(futuro);
        turnos.Turnos.Add(pasado);
        var publicador = new PublicadorFalso();
        var servicio = CrearServicio(empleados, new RepositorioCargosEnMemoria(cargoId), turnos, publicador);

        var resultado = await servicio.CambiarEstadoAsync(id, false, default);

        Assert.False(resultado.Value!.Activo);
        Assert.Equal(EstadoTurno.Cancelado, futuro.Estado);
        Assert.Equal(EstadoTurno.Programado, pasado.Estado);
        Assert.Single(publicador.Publicados);
    }

    private static Turno Turno(Guid empleadoId, DateOnly fecha, EstadoTurno estado) =>
        new() { Id = Guid.NewGuid(), EmpleadoId = empleadoId, CargoId = Guid.NewGuid(), Fecha = fecha,
            HoraInicio = new TimeOnly(9, 0), HoraFin = new TimeOnly(13, 0), Estado = estado };

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }

    private sealed class PublicadorFalso : GIRA.Application.Interfaces.Services.Reservas.IPublicadorEventosSeguro
    {
        public List<object> Publicados { get; } = [];

        public Task PublicarAsync<TEvento>(TEvento evento, string referencia, CancellationToken cancellationToken)
            where TEvento : MediatR.INotification
        {
            Publicados.Add(evento!);
            return Task.CompletedTask;
        }
    }

    /// <summary>Repositorio de turnos falso con los métodos que usa el servicio de empleados.</summary>
    private sealed class RepositorioTurnosFalso : ITurnoRepository
    {
        public List<Turno> Turnos { get; } = [];

        public Task<Turno?> BuscarAsync(Guid id, CancellationToken cancellationToken) => Task.FromResult(Turnos.FirstOrDefault(t => t.Id == id));

        public Task<IReadOnlyList<Turno>> ListarPorEmpleadoYFechaAsync(Guid empleadoId, DateOnly fecha, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Turno>>(Turnos.Where(t => t.EmpleadoId == empleadoId && t.Fecha == fecha).ToList());

        public Task<IReadOnlyList<Turno>> ListarPorEmpleadoEntreAsync(Guid empleadoId, DateOnly desde, DateOnly hasta, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Turno>>(Turnos.Where(t => t.EmpleadoId == empleadoId && t.Fecha >= desde && t.Fecha <= hasta).ToList());

        public Task<IReadOnlyList<Turno>> ListarFuturosActivosAsync(Guid empleadoId, DateOnly desde, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Turno>>(Turnos.Where(t => t.EmpleadoId == empleadoId && t.Fecha >= desde
                && (t.Estado == GIRA.Domain.Enums.Personal.EstadoTurno.Programado || t.Estado == GIRA.Domain.Enums.Personal.EstadoTurno.Confirmado)).ToList());

        public Task<IReadOnlyList<Turno>> ListarEntreAsync(DateOnly desde, DateOnly hasta, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Turno>>(Turnos.Where(t => t.Fecha >= desde && t.Fecha <= hasta).ToList());

        public void Agregar(Turno turno) => Turnos.Add(turno);

        public Task GuardarCambiosAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>Repositorio de empleados en memoria con la misma forma que el de producción.</summary>
    private sealed class RepositorioEmpleadosEnMemoria : IEmpleadoRepository
    {
        public List<Empleado> Empleados { get; } = [];

        public bool FallarAlGuardar { get; set; }

        public Task<IReadOnlyList<Empleado>> ListarAsync(bool soloActivos, CancellationToken cancellationToken)
        {
            IEnumerable<Empleado> consulta = Empleados;
            if (soloActivos)
            {
                consulta = consulta.Where(e => e.Activo);
            }

            return Task.FromResult<IReadOnlyList<Empleado>>(consulta.OrderBy(e => e.Apellido).ThenBy(e => e.Nombre).ToList());
        }

        public Task<Empleado?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Empleados.FirstOrDefault(e => e.Id == id));

        public Task<bool> ExisteCedulaAsync(string cedula, Guid? excluirId, CancellationToken cancellationToken) =>
            Task.FromResult(Empleados.Any(e => e.Cedula == cedula && e.Id != excluirId));

        public void Agregar(Empleado empleado)
        {
            empleado.Id = Guid.NewGuid();
            Empleados.Add(empleado);
        }

        public Task GuardarCambiosAsync(CancellationToken cancellationToken)
        {
            if (FallarAlGuardar)
            {
                throw new EmpleadoCedulaDuplicadaException("Choque simulado con el índice único.", new Exception());
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>Repositorio de cargos en memoria que solo conoce un cargo existente.</summary>
    private sealed class RepositorioCargosEnMemoria(Guid cargoExistente) : ICargoRepository
    {
        public Task<IReadOnlyList<Cargo>> ListarAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Cargo>>([]);

        public Task<Cargo?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(id == cargoExistente ? new Cargo { Id = id, Nombre = "Mesero" } : null);

        public Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public void Agregar(Cargo cargo) { }

        public Task GuardarCambiosAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
