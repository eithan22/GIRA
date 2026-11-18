using System.Net;
using System.Net.Http.Json;
using GIRA.Application.DTOs.Personal;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Domain.Constants;
using GIRA.Domain.Enums.Personal;
using GIRA.Tests.Api;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Tests.Personal;

/// <summary>
/// Pruebas HTTP de /api/turnos (GI-331): permisos por rol, asignación dentro de disponibilidad,
/// solapamiento, horario fuera de disponibilidad y cancelación. La base es en memoria por prueba.
/// </summary>
public class TurnosEndpointsTests : IAsyncLifetime
{
    private const string Ruta = "/api/turnos";

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
    public async Task Supervisor_AsignaUnTurnoDentroDeDisponibilidad()
    {
        var empleadoId = await PrepararEmpleadoAsync();
        Como(Roles.Supervisor);

        var respuesta = await _cliente.PostAsJsonAsync(Ruta, Turno(empleadoId, "09:00", "13:00"));

        Assert.Equal(HttpStatusCode.Created, respuesta.StatusCode);
    }

    [Fact]
    public async Task TurnoSolapado_RecibeConflict()
    {
        var empleadoId = await PrepararEmpleadoAsync();
        Como(Roles.Supervisor);
        await _cliente.PostAsJsonAsync(Ruta, Turno(empleadoId, "09:00", "13:00"));

        var solapado = await _cliente.PostAsJsonAsync(Ruta, Turno(empleadoId, "12:00", "15:00"));

        Assert.Equal(HttpStatusCode.Conflict, solapado.StatusCode);
    }

    [Fact]
    public async Task FueraDeDisponibilidad_RecibeBadRequest()
    {
        var empleadoId = await PrepararEmpleadoAsync();
        Como(Roles.Supervisor);

        var fuera = await _cliente.PostAsJsonAsync(Ruta, Turno(empleadoId, "19:00", "21:00"));

        Assert.Equal(HttpStatusCode.BadRequest, fuera.StatusCode);
    }

    [Fact]
    public async Task Cancelar_DejaEstadoCancelado_YSoloAceptaCancelarPorAhora()
    {
        var empleadoId = await PrepararEmpleadoAsync();
        Como(Roles.Supervisor);
        var creado = await (await _cliente.PostAsJsonAsync(Ruta, Turno(empleadoId, "09:00", "13:00")))
            .Content.ReadFromJsonAsync<TurnoDto>();

        var completado = await _cliente.PatchAsJsonAsync($"{Ruta}/{creado!.Id}/estado", new CambiarEstadoTurnoRequest(EstadoTurno.Completado));
        var cancelado = await _cliente.PatchAsJsonAsync($"{Ruta}/{creado.Id}/estado", new CambiarEstadoTurnoRequest(EstadoTurno.Cancelado));

        Assert.Equal(HttpStatusCode.BadRequest, completado.StatusCode);
        Assert.Equal(HttpStatusCode.OK, cancelado.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Mesero)]
    [InlineData(Roles.Cocina)]
    public async Task RolSinAsignarTurnos_RecibeForbidden(string rol)
    {
        Como(rol);

        var respuesta = await _cliente.PostAsJsonAsync(Ruta, Turno(Guid.NewGuid(), "09:00", "13:00"));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    private TurnoRequest Turno(Guid empleadoId, string inicio, string fin) =>
        new(empleadoId, _cargoId, ProximoLunes(), TimeOnly.Parse(inicio), TimeOnly.Parse(fin), null);

    private Guid _cargoId;

    /// <summary>Un lunes dentro de dos semanas, para que la fecha no quede en el pasado.</summary>
    private static DateOnly ProximoLunes()
    {
        var fecha = DateOnly.FromDateTime(DateTime.Today).AddDays(14);
        while (fecha.DayOfWeek != DayOfWeek.Monday)
        {
            fecha = fecha.AddDays(1);
        }

        return fecha;
    }

    /// <summary>Crea un cargo, un empleado activo y disponibilidad los lunes de 08:00 a 18:00.</summary>
    private async Task<Guid> PrepararEmpleadoAsync()
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
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        var token = tokens.GenerarAccessToken(Guid.NewGuid(), "personal@gira.local", "Personal de Prueba", [rol]).Token;
        _cliente.UsarToken(token);
    }
}
