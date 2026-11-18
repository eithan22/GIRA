using System.Net;
using System.Net.Http.Json;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Application.Services.Personal;
using GIRA.Application.Validations.Personal;
using GIRA.Domain.Constants;
using GIRA.Domain.Entities.Personal;
using GIRA.Domain.Enums.Personal;
using GIRA.Tests.Api;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Tests.Personal;

/// <summary>
/// Pruebas de ausencias (GI-357): servicio con repositorios en memoria y endpoints HTTP (permisos, fechas,
/// aprobación única, y que una ausencia aprobada bloquea la asignación de turnos).
/// </summary>
public class AusenciasTests : IAsyncLifetime
{
    private static readonly Guid Ana = Guid.NewGuid();
    private static readonly DateOnly Desde = new(2026, 12, 7);

    private readonly GiraApiFactory _factory = new();
    private HttpClient _cliente = null!;

    public Task InitializeAsync()
    {
        _cliente = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public Task DisposeAsync()
    {
        _cliente.Dispose();
        _factory.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Servicio_Crear_RegistraSolicitudPendiente()
    {
        var repo = new RepositorioAusenciasEnMemoria();
        var servicio = Servicio(repo);

        var resultado = await servicio.CrearAsync(new AusenciaRequest(Ana, Desde, Desde.AddDays(2), TipoAusencia.Vacaciones, "Viaje"), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(EstadoAusencia.Solicitada, resultado.Value!.Estado);
        Assert.Single(repo.Ausencias);
    }

    [Fact]
    public async Task Servicio_FechasInvertidas_DevuelveValidacion()
    {
        var servicio = Servicio(new RepositorioAusenciasEnMemoria());

        var resultado = await servicio.CrearAsync(new AusenciaRequest(Ana, Desde.AddDays(2), Desde, TipoAusencia.Permiso, null), default);

        Assert.Equal(ErrorType.Validation, resultado.Type);
    }

    [Fact]
    public async Task Servicio_EmpleadoInexistente_DevuelveNoEncontrado()
    {
        var servicio = Servicio(new RepositorioAusenciasEnMemoria(), empleadoExiste: false);

        var resultado = await servicio.CrearAsync(new AusenciaRequest(Ana, Desde, Desde, TipoAusencia.Licencia, null), default);

        Assert.Equal(ErrorType.NotFound, resultado.Type);
    }

    [Fact]
    public async Task Servicio_Aprobar_GuardaQuienLaResolvio()
    {
        var repo = new RepositorioAusenciasEnMemoria();
        var ausencia = new Ausencia { Id = Guid.NewGuid(), EmpleadoId = Ana, FechaDesde = Desde, FechaHasta = Desde, Tipo = TipoAusencia.Permiso };
        repo.Ausencias.Add(ausencia);
        var gerente = Guid.NewGuid();

        var resultado = await Servicio(repo).ResolverAsync(ausencia.Id, EstadoAusencia.Aprobada, gerente, default);

        Assert.Equal(EstadoAusencia.Aprobada, resultado.Value!.Estado);
        Assert.Equal(gerente, resultado.Value.AprobadaPor);
    }

    [Fact]
    public async Task Servicio_ResolverDosVeces_DevuelveConflicto()
    {
        var repo = new RepositorioAusenciasEnMemoria();
        var ausencia = new Ausencia { Id = Guid.NewGuid(), EmpleadoId = Ana, FechaDesde = Desde, FechaHasta = Desde, Tipo = TipoAusencia.Permiso };
        repo.Ausencias.Add(ausencia);
        var servicio = Servicio(repo);
        await servicio.ResolverAsync(ausencia.Id, EstadoAusencia.Rechazada, Guid.NewGuid(), default);

        var segunda = await servicio.ResolverAsync(ausencia.Id, EstadoAusencia.Aprobada, Guid.NewGuid(), default);

        Assert.Equal(ErrorType.Conflict, segunda.Type);
        Assert.Equal(EstadoAusencia.Rechazada, ausencia.Estado);
    }

    [Fact]
    public async Task Servicio_ResolverConEstadoSolicitada_DevuelveValidacion()
    {
        var repo = new RepositorioAusenciasEnMemoria();
        var ausencia = new Ausencia { Id = Guid.NewGuid(), EmpleadoId = Ana, FechaDesde = Desde, FechaHasta = Desde, Tipo = TipoAusencia.Permiso };
        repo.Ausencias.Add(ausencia);

        var resultado = await Servicio(repo).ResolverAsync(ausencia.Id, EstadoAusencia.Solicitada, Guid.NewGuid(), default);

        Assert.Equal(ErrorType.Validation, resultado.Type);
    }

    [Fact]
    public async Task Http_GerenteRegistraAprobaYListaUnaAusencia()
    {
        Como(Roles.Gerente);
        var empleadoId = await CrearEmpleadoAsync();

        var creada = await (await _cliente.PostAsJsonAsync("/api/ausencias", new AusenciaRequest(empleadoId, ProximoLunes(), ProximoLunes().AddDays(1), TipoAusencia.Vacaciones, null)))
            .Content.ReadFromJsonAsync<AusenciaDto>();
        var aprobada = await _cliente.PatchAsJsonAsync($"/api/ausencias/{creada!.Id}/estado", new CambiarEstadoAusenciaRequest(EstadoAusencia.Aprobada));
        var lista = await _cliente.GetFromJsonAsync<List<AusenciaDto>>($"/api/ausencias?empleadoId={empleadoId}");

        Assert.Equal(HttpStatusCode.OK, aprobada.StatusCode);
        Assert.Single(lista!);
        Assert.Equal(EstadoAusencia.Aprobada, lista![0].Estado);
    }

    [Fact]
    public async Task Http_FechasInvertidas_RecibeBadRequest()
    {
        Como(Roles.Gerente);
        var empleadoId = await CrearEmpleadoAsync();

        var respuesta = await _cliente.PostAsJsonAsync("/api/ausencias", new AusenciaRequest(empleadoId, ProximoLunes().AddDays(3), ProximoLunes(), TipoAusencia.Permiso, null));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Http_AusenciaAprobada_BloqueaLaAsignacionDeTurnos()
    {
        Como(Roles.Gerente);
        var empleadoId = await CrearEmpleadoAsync();
        var lunes = ProximoLunes();
        var ausencia = await (await _cliente.PostAsJsonAsync("/api/ausencias", new AusenciaRequest(empleadoId, lunes, lunes, TipoAusencia.Licencia, null)))
            .Content.ReadFromJsonAsync<AusenciaDto>();
        await _cliente.PatchAsJsonAsync($"/api/ausencias/{ausencia!.Id}/estado", new CambiarEstadoAusenciaRequest(EstadoAusencia.Aprobada));

        Como(Roles.Supervisor);
        var turno = await _cliente.PostAsJsonAsync("/api/turnos", new TurnoRequest(empleadoId, _cargoId, lunes, new TimeOnly(9, 0), new TimeOnly(13, 0), null));

        Assert.Equal(HttpStatusCode.Conflict, turno.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Mesero)]
    [InlineData(Roles.Supervisor)]
    public async Task Http_RolSinPermiso_RecibeForbidden(string rol)
    {
        Como(rol);

        Assert.Equal(HttpStatusCode.Forbidden, (await _cliente.GetAsync("/api/ausencias")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _cliente.PostAsJsonAsync("/api/ausencias", new AusenciaRequest(Guid.NewGuid(), Desde, Desde, TipoAusencia.Permiso, null))).StatusCode);
    }

    private Guid _cargoId;

    private static DateOnly ProximoLunes()
    {
        var fecha = DateOnly.FromDateTime(DateTime.Today).AddDays(14);
        while (fecha.DayOfWeek != DayOfWeek.Monday)
        {
            fecha = fecha.AddDays(1);
        }

        return fecha;
    }

    private async Task<Guid> CrearEmpleadoAsync()
    {
        Como(Roles.Gerente);
        var cargo = await (await _cliente.PostAsJsonAsync("/api/cargos", new CargoRequest("Mesero", null)))
            .Content.ReadFromJsonAsync<CargoDto>();
        _cargoId = cargo!.Id;

        var empleado = await (await _cliente.PostAsJsonAsync("/api/empleados",
                new EmpleadoRequest("Ana", "Pérez", "001-0000001-1", null, null, cargo.Id, new DateOnly(2026, 1, 15), 40)))
            .Content.ReadFromJsonAsync<EmpleadoDto>();

        await _cliente.PostAsJsonAsync($"/api/empleados/{empleado!.Id}/disponibilidad",
            new DisponibilidadRequest(DayOfWeek.Monday, new TimeOnly(8, 0), new TimeOnly(18, 0)));

        return empleado.Id;
    }

    private void Como(string rol)
    {
        using var scope = _factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<GIRA.Application.Interfaces.Services.Seguridad.Auth.ITokenService>();
        var token = tokens.GenerarAccessToken(Guid.NewGuid(), "personal@gira.local", "Personal de Prueba", [rol]).Token;
        _cliente.UsarToken(token);
    }

    private static AusenciaService Servicio(RepositorioAusenciasEnMemoria repo, bool empleadoExiste = true) =>
        new(repo, new RepositorioEmpleadosFalso(empleadoExiste), new AusenciaRequestValidator());

    private sealed class RepositorioAusenciasEnMemoria : IAusenciaRepository
    {
        public List<Ausencia> Ausencias { get; } = [];

        public Task<IReadOnlyList<Ausencia>> ListarAsync(Guid? empleadoId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Ausencia>>(Ausencias.Where(a => empleadoId is null || a.EmpleadoId == empleadoId).ToList());

        public Task<Ausencia?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(Ausencias.FirstOrDefault(a => a.Id == id));

        public void Agregar(Ausencia ausencia)
        {
            ausencia.Id = Guid.NewGuid();
            Ausencias.Add(ausencia);
        }

        public Task GuardarCambiosAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public Task<bool> TieneAprobadaEnFechaAsync(Guid empleadoId, DateOnly fecha, CancellationToken cancellationToken) =>
            Task.FromResult(false);
    }

    private sealed class RepositorioEmpleadosFalso(bool existe) : IEmpleadoRepository
    {
        public Task<IReadOnlyList<Empleado>> ListarAsync(bool soloActivos, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<Empleado>>([]);

        public Task<Empleado?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult<Empleado?>(existe ? new Empleado { Id = id, Activo = true } : null);

        public Task<bool> ExisteCedulaAsync(string cedula, Guid? excluirId, CancellationToken cancellationToken) => Task.FromResult(false);
        public void Agregar(Empleado empleado) { }
        public Task GuardarCambiosAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
