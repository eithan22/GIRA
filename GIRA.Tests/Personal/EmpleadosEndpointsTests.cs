using System.Net;
using System.Net.Http.Json;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Domain.Constants;
using GIRA.Tests.Api;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Tests.Personal;

/// <summary>
/// Pruebas HTTP de /api/empleados (GI-317): permisos por rol, alta con cargo existente, cédula repetida,
/// y activar o desactivar sin eliminar. La base es en memoria por prueba.
/// </summary>
public class EmpleadosEndpointsTests : IAsyncLifetime
{
    private const string Ruta = "/api/empleados";
    private const string RutaCargos = "/api/cargos";

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
    public async Task Gerente_RegistraEmpleadoConUnCargoExistente()
    {
        Como(Roles.Gerente);
        var cargo = await CrearCargoAsync("Mesero");

        var respuesta = await _cliente.PostAsJsonAsync(Ruta, Datos(cargo.Id));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
        var empleado = await respuesta.Content.ReadFromJsonAsync<EmpleadoDto>();
        Assert.True(empleado!.Activo);
    }

    [Fact]
    public async Task CedulaRepetida_RecibeConflict()
    {
        Como(Roles.Gerente);
        var cargo = await CrearCargoAsync("Cocinero");
        await _cliente.PostAsJsonAsync(Ruta, Datos(cargo.Id));

        var repetida = await _cliente.PostAsJsonAsync(Ruta, Datos(cargo.Id));

        Assert.Equal(HttpStatusCode.Conflict, repetida.StatusCode);
    }

    [Fact]
    public async Task CargoInexistente_RecibeBadRequest()
    {
        Como(Roles.Gerente);

        var respuesta = await _cliente.PostAsJsonAsync(Ruta, Datos(Guid.NewGuid()));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task DesactivarEmpleado_NoLoElimina_YSaleDeLosActivos()
    {
        Como(Roles.Gerente);
        var cargo = await CrearCargoAsync("Bartender");
        var creado = await (await _cliente.PostAsJsonAsync(Ruta, Datos(cargo.Id))).Content.ReadFromJsonAsync<EmpleadoDto>();

        var desactivado = await _cliente.PatchAsJsonAsync($"{Ruta}/{creado!.Id}/estado", new CambiarEstadoEmpleadoRequest(false));
        var activos = await _cliente.GetFromJsonAsync<List<EmpleadoDto>>($"{Ruta}?soloActivos=true");
        var todos = await _cliente.GetFromJsonAsync<List<EmpleadoDto>>(Ruta);

        Assert.Equal(HttpStatusCode.OK, desactivado.StatusCode);
        Assert.DoesNotContain(activos!, e => e.Id == creado.Id);
        Assert.Contains(todos!, e => e.Id == creado.Id && !e.Activo);
    }

    [Theory]
    [InlineData(Roles.Mesero)]
    [InlineData(Roles.Supervisor)]
    [InlineData(Roles.Cocina)]
    public async Task RolSinPermiso_RecibeForbidden(string rol)
    {
        Como(rol);

        Assert.Equal(HttpStatusCode.Forbidden, (await _cliente.GetAsync(Ruta)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _cliente.PostAsJsonAsync(Ruta, Datos(Guid.NewGuid()))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _cliente.PatchAsJsonAsync($"{Ruta}/{Guid.NewGuid()}/estado", new CambiarEstadoEmpleadoRequest(false))).StatusCode);
    }

    [Fact]
    public async Task SinToken_RecibeUnauthorized()
    {
        Assert.Equal(HttpStatusCode.Unauthorized, (await _cliente.GetAsync(Ruta)).StatusCode);
    }

    private async Task<CargoDto> CrearCargoAsync(string nombre)
    {
        var respuesta = await _cliente.PostAsJsonAsync(RutaCargos, new CargoRequest(nombre, null));
        return (await respuesta.Content.ReadFromJsonAsync<CargoDto>())!;
    }

    private static EmpleadoRequest Datos(Guid cargoId) =>
        new("Ana", "Pérez", "001-0000001-1", null, null, cargoId, new DateOnly(2026, 1, 15), 40);

    private void Como(string rol)
    {
        using var scope = _factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var token = tokens.GenerarAccessToken(Guid.NewGuid(), "personal@gira.local", "Personal de Prueba", [rol]).Token;
        _cliente.UsarToken(token);
    }
}
