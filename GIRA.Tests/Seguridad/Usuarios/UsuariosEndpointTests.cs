using System.Net;
using System.Text.Json;
using GIRA.Domain.Constants;
using GIRA.Tests.Api;

namespace GIRA.Tests.Seguridad.Usuarios;

public class UsuariosEndpointTests : IAsyncLifetime
{
    private const string Password = "Clave-Segura1";
    private const string CorreoAdmin = "admin@gira.local";
    private const string CorreoGerente = "gerente@gira.local";

    private readonly GiraApiFactory _factory = new();
    private HttpClient _cliente = null!;

    public async Task InitializeAsync()
    {
        _cliente = _factory.CreateClient();
        await _factory.CrearUsuarioAsync(CorreoAdmin, Password, true, Roles.Administrador);
        await _factory.CrearUsuarioAsync(CorreoGerente, Password, true, Roles.Gerente);
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Usuarios_SinToken_Devuelve401()
    {
        var respuesta = await _cliente.GetAsync("/api/usuarios");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Usuarios_ComoGerente_Devuelve403()
    {
        _cliente.UsarToken(await _cliente.ObtenerTokenAsync(CorreoGerente, Password));

        var respuesta = await _cliente.GetAsync("/api/usuarios");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Usuarios_ComoAdministrador_Devuelve200()
    {
        _cliente.UsarToken(await _cliente.ObtenerTokenAsync(CorreoAdmin, Password));

        var respuesta = await _cliente.GetAsync("/api/usuarios");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task ObtenerUsuario_SinToken_Devuelve401()
    {
        var respuesta = await _cliente.GetAsync($"/api/usuarios/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task ObtenerUsuario_ComoGerente_Devuelve403()
    {
        _cliente.UsarToken(await _cliente.ObtenerTokenAsync(CorreoGerente, Password));

        var respuesta = await _cliente.GetAsync($"/api/usuarios/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task ObtenerUsuario_IdInexistente_Devuelve404()
    {
        _cliente.UsarToken(await _cliente.ObtenerTokenAsync(CorreoAdmin, Password));

        var respuesta = await _cliente.GetAsync($"/api/usuarios/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Fact]
    public async Task ObtenerUsuario_IdValido_Devuelve200ConLosDatosDelUsuario()
    {
        _cliente.UsarToken(await _cliente.ObtenerTokenAsync(CorreoAdmin, Password));

        var listado = await _cliente.GetAsync("/api/usuarios");
        using var json = JsonDocument.Parse(await listado.Content.ReadAsStringAsync());
        var idGerente = json.RootElement.GetProperty("items").EnumerateArray()
            .First(u => u.GetProperty("email").GetString() == CorreoGerente)
            .GetProperty("id").GetString();

        var respuesta = await _cliente.GetAsync($"/api/usuarios/{idGerente}");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var detalle = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(CorreoGerente, detalle.RootElement.GetProperty("email").GetString());
    }

    [Fact]
    public async Task Me_SinToken_Devuelve401()
    {
        var respuesta = await _cliente.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Me_ConTokenValido_Devuelve200ConLosDatosDelUsuario()
    {
        _cliente.UsarToken(await _cliente.ObtenerTokenAsync(CorreoGerente, Password));

        var respuesta = await _cliente.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        using var json = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(CorreoGerente, json.RootElement.GetProperty("email").GetString());
        Assert.Contains(
            json.RootElement.GetProperty("roles").EnumerateArray(),
            r => r.GetString() == Roles.Gerente);
    }
}
