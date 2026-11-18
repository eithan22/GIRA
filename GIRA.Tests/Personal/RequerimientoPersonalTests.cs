using System.Net;
using System.Net.Http.Json;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.Interfaces.Repository.Personal;
using GIRA.Application.Services.Personal;
using GIRA.Application.Validations.Personal;
using GIRA.Domain.Constants;
using GIRA.Domain.Entities.Personal;
using GIRA.Tests.Api;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Tests.Personal;

/// <summary>
/// Pruebas del requerimiento de personal por cargo (GI-344): servicio con repositorio en memoria y
/// endpoints HTTP (permisos, crear y actualizar, valores no válidos).
/// </summary>
public class RequerimientoPersonalTests : IAsyncLifetime
{
    private static readonly Guid Mesero = Guid.NewGuid();

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
    public async Task Servicio_Guardar_CreaYLuegoActualizaElMismoCargo()
    {
        var repo = new RepositorioRequerimientosEnMemoria();
        var servicio = new RequerimientoPersonalService(repo, new RepositorioCargosFalso(Mesero), new RequerimientoPersonalRequestValidator());

        await servicio.GuardarAsync(new RequerimientoPersonalRequest(Mesero, 20, 2), default);
        var actualizado = await servicio.GuardarAsync(new RequerimientoPersonalRequest(Mesero, 15, 3), default);

        Assert.True(actualizado.IsSuccess);
        Assert.Single(repo.Requerimientos);
        Assert.Equal(15, repo.Requerimientos.Single().ComensalesPorEmpleado);
        Assert.Equal(3, repo.Requerimientos.Single().MinimoPorFranja);
    }

    [Fact]
    public async Task Servicio_ValoresEnCero_DevuelveValidacion()
    {
        var servicio = new RequerimientoPersonalService(new RepositorioRequerimientosEnMemoria(), new RepositorioCargosFalso(Mesero), new RequerimientoPersonalRequestValidator());

        var resultado = await servicio.GuardarAsync(new RequerimientoPersonalRequest(Mesero, 0, 2), default);

        Assert.Equal(ErrorType.Validation, resultado.Type);
    }

    [Fact]
    public async Task Servicio_CargoInexistente_DevuelveValidacion()
    {
        var servicio = new RequerimientoPersonalService(new RepositorioRequerimientosEnMemoria(), new RepositorioCargosFalso(Guid.NewGuid()), new RequerimientoPersonalRequestValidator());

        var resultado = await servicio.GuardarAsync(new RequerimientoPersonalRequest(Mesero, 20, 2), default);

        Assert.Equal(ErrorType.Validation, resultado.Type);
    }

    [Fact]
    public async Task Http_GerenteConfiguraYListaElRequerimiento()
    {
        Como(Roles.Gerente);
        var cargo = await (await _cliente.PostAsJsonAsync("/api/cargos", new CargoRequest("Mesero", null)))
            .Content.ReadFromJsonAsync<CargoDto>();

        var guardado = await _cliente.PutAsJsonAsync("/api/requerimientos-personal", new RequerimientoPersonalRequest(cargo!.Id, 20, 2));
        var lista = await _cliente.GetFromJsonAsync<List<RequerimientoPersonalDto>>("/api/requerimientos-personal");

        Assert.Equal(HttpStatusCode.OK, guardado.StatusCode);
        Assert.Single(lista!);
        Assert.Equal(20, lista![0].ComensalesPorEmpleado);
    }

    [Fact]
    public async Task Http_ValoresEnCero_RecibeBadRequest()
    {
        Como(Roles.Gerente);
        var cargo = await (await _cliente.PostAsJsonAsync("/api/cargos", new CargoRequest("Cocinero", null)))
            .Content.ReadFromJsonAsync<CargoDto>();

        var respuesta = await _cliente.PutAsJsonAsync("/api/requerimientos-personal", new RequerimientoPersonalRequest(cargo!.Id, 0, 2));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Mesero)]
    [InlineData(Roles.Supervisor)]
    public async Task Http_RolSinPermiso_RecibeForbidden(string rol)
    {
        Como(rol);

        Assert.Equal(HttpStatusCode.Forbidden, (await _cliente.GetAsync("/api/requerimientos-personal")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _cliente.PutAsJsonAsync("/api/requerimientos-personal", new RequerimientoPersonalRequest(Guid.NewGuid(), 20, 2))).StatusCode);
    }

    private void Como(string rol)
    {
        using var scope = _factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<GIRA.Application.Interfaces.Services.Seguridad.Auth.ITokenService>();
        var token = tokens.GenerarAccessToken(Guid.NewGuid(), "personal@gira.local", "Personal de Prueba", [rol]).Token;
        _cliente.UsarToken(token);
    }

    /// <summary>Repositorio de requerimientos en memoria con la misma forma que el de producción.</summary>
    private sealed class RepositorioRequerimientosEnMemoria : IRequerimientoPersonalRepository
    {
        public List<RequerimientoPersonal> Requerimientos { get; } = [];

        public Task<IReadOnlyList<RequerimientoPersonal>> ListarAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<RequerimientoPersonal>>(Requerimientos.ToList());

        public Task<RequerimientoPersonal?> BuscarPorCargoAsync(Guid cargoId, CancellationToken cancellationToken) =>
            Task.FromResult(Requerimientos.FirstOrDefault(r => r.CargoId == cargoId));

        public void Agregar(RequerimientoPersonal requerimiento)
        {
            requerimiento.Id = Guid.NewGuid();
            Requerimientos.Add(requerimiento);
        }

        public Task GuardarCambiosAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    /// <summary>Repositorio de cargos falso que solo conoce un cargo.</summary>
    private sealed class RepositorioCargosFalso(Guid conocido) : ICargoRepository
    {
        public Task<IReadOnlyList<Cargo>> ListarAsync(CancellationToken cancellationToken) => Task.FromResult<IReadOnlyList<Cargo>>([]);
        public Task<Cargo?> BuscarAsync(Guid id, CancellationToken cancellationToken) =>
            Task.FromResult(id == conocido ? new Cargo { Id = id, Nombre = "Mesero" } : null);
        public Task<bool> ExisteNombreAsync(string nombre, Guid? excluirId, CancellationToken cancellationToken) => Task.FromResult(false);
        public void Agregar(Cargo cargo) { }
        public Task GuardarCambiosAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }
}
