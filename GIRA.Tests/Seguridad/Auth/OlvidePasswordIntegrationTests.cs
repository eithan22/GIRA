using System.Net;
using System.Net.Http.Json;
using GIRA.Api.Common.Seguridad;
using GIRA.Application.DTOs.Seguridad.Auth;
using GIRA.Domain.Constants;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Tests.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Tests.Seguridad.Auth;

/// <summary>
/// GI-217: Pruebas de integración de POST /api/auth/olvide-password: recorren el pipeline HTTP
/// real (rate limiting, validación y persistencia) contra una base en memoria por prueba.
/// </summary>
public class OlvidePasswordIntegrationTests : IAsyncLifetime
{
    private const string Url = "/api/auth/olvide-password";
    private const string Correo = "ana.perez@gira.local";
    private const string Password = "Clave-Segura1";

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

    private Task<HttpResponseMessage> SolicitarAsync(string correo) =>
        _cliente.PostAsJsonAsync(Url, new OlvidePasswordRequest { Email = correo });

    private async Task<List<Guid>> UsuariosConSolicitudAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        return await db.SolicitudesRecuperacionPassword
            .AsNoTracking()
            .Select(s => s.UsuarioId)
            .ToListAsync();
    }

    // Correo de un usuario activo -> 200 y queda una solicitud para ese usuario.
    [Fact]
    public async Task OlvidePassword_CorreoActivo_Devuelve200YCreaLaSolicitud()
    {
        var id = await _factory.CrearUsuarioAsync(Correo, Password, true, Roles.Mesero);

        var respuesta = await SolicitarAsync(Correo);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Equal(new[] { id }, await UsuariosConSolicitudAsync());
    }

    // Correo inexistente -> la misma respuesta 200, sin crear nada (no revela si existe).
    [Fact]
    public async Task OlvidePassword_CorreoInexistente_Devuelve200SinCrearNada()
    {
        var respuesta = await SolicitarAsync("nadie@gira.local");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Empty(await UsuariosConSolicitudAsync());
    }

    // Usuario desactivado -> 200 igual que los demás, sin crear la solicitud.
    [Fact]
    public async Task OlvidePassword_UsuarioDesactivado_Devuelve200SinCrearNada()
    {
        await _factory.CrearUsuarioAsync(Correo, Password, false, Roles.Mesero);

        var respuesta = await SolicitarAsync(Correo);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Empty(await UsuariosConSolicitudAsync());
    }

    // Pedirlo dos veces -> sigue habiendo una sola solicitud para ese usuario.
    [Fact]
    public async Task OlvidePassword_DosVeces_NoDuplicaLaSolicitud()
    {
        var id = await _factory.CrearUsuarioAsync(Correo, Password, true, Roles.Mesero);

        Assert.Equal(HttpStatusCode.OK, (await SolicitarAsync(Correo)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await SolicitarAsync(Correo)).StatusCode);

        Assert.Equal(new[] { id }, await UsuariosConSolicitudAsync());
    }

    // Correo con formato inválido -> 400; el validador corta antes de consultar la base.
    [Fact]
    public async Task OlvidePassword_CorreoConFormatoInvalido_Devuelve400()
    {
        var respuesta = await SolicitarAsync("no-es-un-correo");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    // Más de 3 solicitudes en 10 minutos desde la misma IP -> 429 con el mensaje de esta política.
    [Fact]
    public async Task OlvidePassword_CuartaSolicitudEnDiezMinutos_Devuelve429()
    {
        for (var intento = 0; intento < 3; intento++)
        {
            Assert.Equal(HttpStatusCode.OK, (await SolicitarAsync("nadie@gira.local")).StatusCode);
        }

        var respuesta = await SolicitarAsync("nadie@gira.local");

        Assert.Equal(HttpStatusCode.TooManyRequests, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(LimiteOlvidePasswordExtensions.MensajeRechazo, problema!.Detail);
    }
}
