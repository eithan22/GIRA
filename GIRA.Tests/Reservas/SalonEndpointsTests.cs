using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Domain.Constants;
using GIRA.Tests.Api;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Text;

namespace GIRA.Tests.Reservas;

public class SalonEndpointsTests : IAsyncLifetime
{
    private readonly GiraApiFactory _factory = new();
    private HttpClient _cliente = null!;

    public Task InitializeAsync()
    {
        _cliente = _factory.CreateClient();
        return Task.CompletedTask;
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        await _factory.DisposeAsync();
    }

    private string GenerarToken(params string[] roles)
    {
        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        return tokenService.GenerarAccessToken(Guid.NewGuid(), "personal@gira.local", "Personal de Prueba", roles).Token;
    }

    [Theory]
    [InlineData("/api/zonas")]
    [InlineData("/api/mesas")]
    [InlineData("/api/franjas")]
    public async Task Sin_Token_Responde_401(string url)
    {
        var respuesta = await _cliente.GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("/api/zonas", Roles.Mesero)]
    [InlineData("/api/mesas", Roles.Supervisor)]
    [InlineData("/api/franjas", Roles.Cocina)]
    public async Task Rol_Sin_La_Politica_Responde_403(string url, string rol)
    {
        _cliente.UsarToken(GenerarToken(rol));

        var respuesta = await _cliente.GetAsync(url);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Gerente)]
    [InlineData(Roles.Administrador)]
    public async Task Gerente_Y_Administrador_Listan(string rol)
    {
        _cliente.UsarToken(GenerarToken(rol));

        var respuesta = await _cliente.GetAsync("/api/zonas");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Flujo_Completo_De_Zona_Y_Mesa()
    {
        _cliente.UsarToken(GenerarToken(Roles.Gerente));

        var crearZona = await _cliente.PostAsJsonAsync("/api/zonas", new { nombre = "Terraza" });
        Assert.Equal(HttpStatusCode.OK, crearZona.StatusCode);
        var zona = await crearZona.Content.ReadFromJsonAsync<ZonaDto>();

        var duplicada = await _cliente.PostAsJsonAsync("/api/zonas", new { nombre = "terraza" });
        Assert.Equal(HttpStatusCode.Conflict, duplicada.StatusCode);

        var crearMesa = await _cliente.PostAsJsonAsync("/api/mesas", new { numero = 1, capacidad = 4, zonaId = zona!.Id });
        Assert.Equal(HttpStatusCode.OK, crearMesa.StatusCode);
        var mesa = await crearMesa.Content.ReadFromJsonAsync<MesaDto>();
        Assert.Equal("Terraza", mesa!.ZonaNombre);

        var lista = await _cliente.GetFromJsonAsync<PagedResult<MesaDto>>($"/api/mesas?zonaId={zona.Id}");
        Assert.Equal(1, lista!.Total);

        var desactivar = await _cliente.PatchAsync($"/api/mesas/{mesa.Id}/estado", JsonContent.Create(new { activa = false }));
        Assert.Equal(HttpStatusCode.OK, desactivar.StatusCode);
        var inactivas = await _cliente.GetFromJsonAsync<PagedResult<MesaDto>>("/api/mesas?activa=false");
        Assert.Equal(1, inactivas!.Total);
    }

    [Fact]
    public async Task Franja_Invalida_Responde_400_Y_Solapada_409()
    {
        _cliente.UsarToken(GenerarToken(Roles.Administrador));

        var invalida = await _cliente.PostAsJsonAsync(
            "/api/franjas", new { nombre = "Rara", horaInicio = "15:00:00", horaFin = "12:00:00" });
        Assert.Equal(HttpStatusCode.BadRequest, invalida.StatusCode);

        var ok = await _cliente.PostAsJsonAsync(
            "/api/franjas", new { nombre = "Almuerzo", horaInicio = "12:00:00", horaFin = "15:00:00" });
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);

        var solapada = await _cliente.PostAsJsonAsync(
            "/api/franjas", new { nombre = "Tarde", horaInicio = "14:00:00", horaFin = "17:00:00" });
        Assert.Equal(HttpStatusCode.Conflict, solapada.StatusCode);
    }

    [Fact]
    public async Task Recurso_Inexistente_Responde_404()
    {
        _cliente.UsarToken(GenerarToken(Roles.Gerente));

        var respuesta = await _cliente.GetAsync($"/api/mesas/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}