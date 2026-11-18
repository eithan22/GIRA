using GIRA.Application.Common;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Application.Services.Personal;
using GIRA.Application.Validations.Personal;
using GIRA.Domain.Entities.Personal;

namespace GIRA.Tests.Personal;

/// <summary>
/// Pruebas del servicio de disponibilidad (GI-322): bloques válidos, horas inválidas, solapamiento,
/// bloques contiguos, empleado inexistente y borrado. Usan repositorios en memoria.
/// </summary>
public class DisponibilidadServiceTests
{
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly Guid Luis = Guid.NewGuid();

    [Fact]
    public async Task Agregar_BloqueValido_SeRegistra()
    {
        var repo = new RepositorioDisponibilidadEnMemoria();
        var servicio = CrearServicio(repo);

        var resultado = await servicio.AgregarAsync(Ana, new DisponibilidadRequest(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0)), default);

        Assert.True(resultado.IsSuccess);
        Assert.Single(repo.Bloques);
    }

    [Fact]
    public async Task Agregar_InicioNoAnteriorAlFin_DevuelveValidacion()
    {
        var servicio = CrearServicio(new RepositorioDisponibilidadEnMemoria());

        var resultado = await servicio.AgregarAsync(Ana, new DisponibilidadRequest(DayOfWeek.Monday, new TimeOnly(12, 0), new TimeOnly(8, 0)), default);

        Assert.Equal(ErrorType.Validation, resultado.Type);
    }

    [Fact]
    public async Task Agregar_SolapadoMismoDia_DevuelveConflictoYNoGuarda()
    {
        var repo = new RepositorioDisponibilidadEnMemoria();
        repo.Bloques.Add(new Disponibilidad { Id = Guid.NewGuid(), EmpleadoId = Ana, DiaSemana = DayOfWeek.Monday, HoraInicio = new TimeOnly(8, 0), HoraFin = new TimeOnly(12, 0) });
        var servicio = CrearServicio(repo);

        var resultado = await servicio.AgregarAsync(Ana, new DisponibilidadRequest(DayOfWeek.Monday, new TimeOnly(11, 0), new TimeOnly(15, 0)), default);

        Assert.Equal(ErrorType.Conflict, resultado.Type);
        Assert.Single(repo.Bloques);
    }

    [Fact]
    public async Task Agregar_BloquesContiguos_NoSeSolapan()
    {
        var repo = new RepositorioDisponibilidadEnMemoria();
        repo.Bloques.Add(new Disponibilidad { Id = Guid.NewGuid(), EmpleadoId = Ana, DiaSemana = DayOfWeek.Monday, HoraInicio = new TimeOnly(8, 0), HoraFin = new TimeOnly(12, 0) });
        var servicio = CrearServicio(repo);

        var resultado = await servicio.AgregarAsync(Ana, new DisponibilidadRequest(DayOfWeek.Monday, new TimeOnly(12, 0), new TimeOnly(16, 0)), default);

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public async Task Agregar_MismoHorarioOtroDia_NoEsConflicto()
    {
        var repo = new RepositorioDisponibilidadEnMemoria();
        repo.Bloques.Add(new Disponibilidad { Id = Guid.NewGuid(), EmpleadoId = Ana, DiaSemana = DayOfWeek.Monday, HoraInicio = new TimeOnly(8, 0), HoraFin = new TimeOnly(12, 0) });
        var servicio = CrearServicio(repo);

        var resultado = await servicio.AgregarAsync(Ana, new DisponibilidadRequest(DayOfWeek.Tuesday, new TimeOnly(8, 0), new TimeOnly(12, 0)), default);

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public async Task Agregar_EmpleadoInexistente_DevuelveNoEncontrado()
    {
        var servicio = CrearServicio(new RepositorioDisponibilidadEnMemoria());

        var resultado = await servicio.AgregarAsync(Guid.NewGuid(), new DisponibilidadRequest(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(12, 0)), default);

        Assert.Equal(ErrorType.NotFound, resultado.Type);
    }

    [Fact]
    public async Task Eliminar_BloqueDeOtroEmpleado_DevuelveNoEncontrado()
    {
        var repo = new RepositorioDisponibilidadEnMemoria();
        var id = Guid.NewGuid();
        repo.Bloques.Add(new Disponibilidad { Id = id, EmpleadoId = Ana, DiaSemana = DayOfWeek.Monday, HoraInicio = new TimeOnly(8, 0), HoraFin = new TimeOnly(12, 0) });
        var servicio = CrearServicio(repo);

        var resultado = await servicio.EliminarAsync(Luis, id, default);

        Assert.Equal(ErrorType.NotFound, resultado.Type);
        Assert.Single(repo.Bloques);
    }

    [Fact]
    public async Task Eliminar_BloquePropio_LoQuitaDeLaLista()
    {
        var repo = new RepositorioDisponibilidadEnMemoria();
        var id = Guid.NewGuid();
        repo.Bloques.Add(new Disponibilidad { Id = id, EmpleadoId = Ana, DiaSemana = DayOfWeek.Monday, HoraInicio = new TimeOnly(8, 0), HoraFin = new TimeOnly(12, 0) });
        var servicio = CrearServicio(repo);

        var resultado = await servicio.EliminarAsync(Ana, id, default);
        var lista = await servicio.ListarAsync(Ana, default);

        Assert.True(resultado.IsSuccess);
        Assert.Empty(lista.Value!);
    }

    private static DisponibilidadService CrearServicio(RepositorioDisponibilidadEnMemoria repo) =>
        new(repo, new RepositorioEmpleadosFalsos(Ana, Luis), new DisponibilidadRequestValidator());

    /// <summary>Repositorio de disponibilidad en memoria con el mismo comportamiento que el de producción.</summary>
    private sealed class RepositorioDisponibilidadEnMemoria : IDisponibilidadRepository
    {
        public List<Disponibilidad> Bloques { get; } = [];

        public Task<IReadOnlyList<Disponibilidad>> ListarPorEmpleadoAsync(Guid empleadoId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Disponibilidad>>(
                Bloques.Where(d => d.EmpleadoId == empleadoId)
                    .OrderBy(d => d.DiaSemana).ThenBy(d => d.HoraInicio).ToList());

        public Task<Disponibilidad?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Bloques.FirstOrDefault(d => d.Id == id));

        public void Agregar(Disponibilidad disponibilidad)
        {
            disponibilidad.Id = Guid.NewGuid();
            Bloques.Add(disponibilidad);
        }

        public void Eliminar(Disponibilidad disponibilidad) => Bloques.Remove(disponibilidad);

        public Task GuardarCambiosAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>Repositorio de empleados falso que solo conoce los identificadores indicados.</summary>
    private sealed class RepositorioEmpleadosFalsos(params Guid[] conocidos) : IEmpleadoRepository
    {
        public Task<IReadOnlyList<Empleado>> ListarAsync(bool soloActivos, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Empleado>>([]);

        public Task<Empleado?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(conocidos.Contains(id) ? new Empleado { Id = id, Activo = true } : null);

        public Task<bool> ExisteCedulaAsync(string cedula, Guid? excluirId, CancellationToken cancellationToken) =>
            Task.FromResult(false);

        public void Agregar(Empleado empleado) { }

        public Task GuardarCambiosAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
