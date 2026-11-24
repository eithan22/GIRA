using System.Net;
using System.Net.Http.Json;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Domain.Constants;
using GIRA.Tests.Api;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Tests.Personal;

/// <summary>
/// Pruebas HTTP de /api/cargos (GI-312): permisos por rol, creación, nombre repetido y validación.
/// La base es en memoria por prueba, así que no hay datos de una prueba a otra.
/// </summary>
public class CargosEndpointsTests : IAsyncLifetime
{
    private const string Ruta = "/api/cargos";

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
    public async Task Gerente_PuedeCrearListarYEditar()
    {
        Como(Roles.Gerente);

        var creado = await _cliente.PostAsJsonAsync(Ruta, new CargoRequest("Mesero", "Atiende mesas"));
        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
        var cargo = await creado.Content.ReadFromJsonAsync<CargoDto>();

        var lista = await _cliente.GetFromJsonAsync<List<CargoDto>>(Ruta);
        Assert.Contains(lista!, c => c.Nombre == "Mesero");

        var editado = await _cliente.PutAsJsonAsync($"{Ruta}/{cargo!.Id}", new CargoRequest("Mesero senior", null));
        Assert.Equal(HttpStatusCode.OK, editado.StatusCode);
    }

    [Fact]
    public async Task Administrador_PuedeCrearUnCargo()
    {
        Como(Roles.Administrador);

        var respuesta = await _cliente.PostAsJsonAsync(Ruta, new CargoRequest("Bartender", null));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Mesero)]
    [InlineData(Roles.Cocina)]
    public async Task RolSinPermiso_RecibeForbidden(string rol)
    {
        Como(rol);

        Assert.Equal(HttpStatusCode.Forbidden, (await _cliente.GetAsync(Ruta)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _cliente.PostAsJsonAsync(Ruta, new CargoRequest("Anfitrión", null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _cliente.PutAsJsonAsync($"{Ruta}/{Guid.NewGuid()}", new CargoRequest("Anfitrión", null))).StatusCode);
    }

    [Fact]
    public async Task Supervisor_PuedeLeerCargosPeroNoModificarlos()
    {
        // El Supervisor necesita la lista de cargos para planificar (LeerPersonal), pero no crearlos ni editarlos.
        Como(Roles.Supervisor);

        Assert.Equal(HttpStatusCode.OK, (await _cliente.GetAsync(Ruta)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _cliente.PostAsJsonAsync(Ruta, new CargoRequest("Anfitrión", null))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _cliente.PutAsJsonAsync($"{Ruta}/{Guid.NewGuid()}", new CargoRequest("Anfitrión", null))).StatusCode);
    }

    [Fact]
    public async Task SinToken_RecibeUnauthorized()
    {
        var respuesta = await _cliente.GetAsync(Ruta);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task NombreRepetido_RecibeConflict()
    {
        Como(Roles.Gerente);
        await _cliente.PostAsJsonAsync(Ruta, new CargoRequest("Cocinero", null));

        var repetido = await _cliente.PostAsJsonAsync(Ruta, new CargoRequest("Cocinero", null));

        Assert.Equal(HttpStatusCode.Conflict, repetido.StatusCode);
    }

    [Fact]
    public async Task NombreVacio_RecibeBadRequest()
    {
        Como(Roles.Gerente);

        var respuesta = await _cliente.PostAsJsonAsync(Ruta, new CargoRequest("   ", null));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    private void Como(string rol)
    {
        using var scope = _factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var token = tokens.GenerarAccessToken(Guid.NewGuid(), "personal@gira.local", "Personal de Prueba", [rol]).Token;
        _cliente.UsarToken(token);
    }
}
