using System.Net;
using System.Net.Http.Json;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Domain.Constants;
using GIRA.Tests.Api;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Tests.Personal;

/// <summary>
/// Pruebas HTTP de /api/empleados/{id}/disponibilidad (GI-322): permisos por rol, alta, solapamiento y borrado.
/// La base es en memoria por prueba.
/// </summary>
public class DisponibilidadEndpointsTests : IAsyncLifetime
{
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
    public async Task Gerente_RegistraYListaUnBloque()
    {
        Como(Roles.Gerente);
        var empleadoId = await CrearEmpleadoAsync();

        var creado = await _cliente.PostAsJsonAsync(Ruta(empleadoId), Bloque(DayOfWeek.Monday, "08:00:00", "12:00:00"));
        var lista = await _cliente.GetFromJsonAsync<List<DisponibilidadDto>>(Ruta(empleadoId));

        Assert.Equal(HttpStatusCode.Created, creado.StatusCode);
        Assert.Single(lista!);
    }

    [Fact]
    public async Task BloqueSolapado_RecibeConflict()
    {
        Como(Roles.Gerente);
        var empleadoId = await CrearEmpleadoAsync();
        await _cliente.PostAsJsonAsync(Ruta(empleadoId), Bloque(DayOfWeek.Monday, "08:00:00", "12:00:00"));

        var solapado = await _cliente.PostAsJsonAsync(Ruta(empleadoId), Bloque(DayOfWeek.Monday, "11:00:00", "15:00:00"));

        Assert.Equal(HttpStatusCode.Conflict, solapado.StatusCode);
    }

    [Fact]
    public async Task Gerente_PuedeEliminarUnBloque()
    {
        Como(Roles.Gerente);
        var empleadoId = await CrearEmpleadoAsync();
        var creado = await (await _cliente.PostAsJsonAsync(Ruta(empleadoId), Bloque(DayOfWeek.Friday, "18:00:00", "22:00:00")))
            .Content.ReadFromJsonAsync<DisponibilidadDto>();

        var borrado = await _cliente.DeleteAsync($"{Ruta(empleadoId)}/{creado!.Id}");
        var lista = await _cliente.GetFromJsonAsync<List<DisponibilidadDto>>(Ruta(empleadoId));

        Assert.Equal(HttpStatusCode.NoContent, borrado.StatusCode);
        Assert.Empty(lista!);
    }

    [Theory]
    [InlineData(Roles.Mesero)]
    [InlineData(Roles.Supervisor)]
    [InlineData(Roles.Cocina)]
    public async Task RolSinPermiso_RecibeForbidden(string rol)
    {
        Como(rol);
        var empleadoId = Guid.NewGuid();

        Assert.Equal(HttpStatusCode.Forbidden, (await _cliente.GetAsync(Ruta(empleadoId))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await _cliente.PostAsJsonAsync(Ruta(empleadoId), Bloque(DayOfWeek.Monday, "08:00:00", "12:00:00"))).StatusCode);
    }

    private static string Ruta(Guid empleadoId) => $"/api/empleados/{empleadoId}/disponibilidad";

    private static DisponibilidadRequest Bloque(DayOfWeek dia, string inicio, string fin) =>
        new(dia, TimeOnly.Parse(inicio), TimeOnly.Parse(fin));

    private async Task<Guid> CrearEmpleadoAsync()
    {
        var cargo = await (await _cliente.PostAsJsonAsync("/api/cargos", new CargoRequest("Mesero", null)))
            .Content.ReadFromJsonAsync<CargoDto>();
        var empleado = await (await _cliente.PostAsJsonAsync("/api/empleados",
                new EmpleadoRequest("Ana", "Pérez", "001-0000001-1", null, null, cargo!.Id, new DateOnly(2026, 1, 15), 40)))
            .Content.ReadFromJsonAsync<EmpleadoDto>();
        return empleado!.Id;
    }

    private void Como(string rol)
    {
        using var scope = _factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var token = tokens.GenerarAccessToken(Guid.NewGuid(), "personal@gira.local", "Personal de Prueba", [rol]).Token;
        _cliente.UsarToken(token);
    }
}
