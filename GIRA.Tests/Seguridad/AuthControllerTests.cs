using System.Net;
using System.Net.Http.Json;
using GIRA.Application.DTOs.Seguridad;
using GIRA.Domain.Constants;
using GIRA.Infrastructure.Identity;
using GIRA.Infrastructure.Services.Seguridad;
using GIRA.Tests.Api;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.JsonWebTokens;

namespace GIRA.Tests.Seguridad;

/// <summary>
/// Pruebas de integración de POST /api/auth/login (GI-117): recorren el pipeline HTTP real
/// (rate limiting, validación, autenticación y mapeo a ProblemDetails) contra una base en memoria.
/// </summary>
public class AuthControllerTests : IAsyncLifetime
{
    private const string Url = "/api/auth/login";
    private const string Correo = "ana.perez@gira.local";
    private const string Password = "Clave-Segura1";
    private const string PasswordIncorrecta = "Otra-Clave1";

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

    private Task<HttpResponseMessage> LoginAsync(string correo, string password)
    {
        return _cliente.PostAsJsonAsync(Url, new LoginRequest { Email = correo, Password = password });
    }

    // Credenciales válidas -> 200 con access token (roles incluidos), refresh token y datos del usuario.
    [Fact]
    public async Task Login_CredencialesValidas_Devuelve200ConTokensYDatos()
    {
        await _factory.CrearUsuarioAsync(Correo, Password, true, Roles.Gerente);

        var respuesta = await LoginAsync(Correo, Password);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(cuerpo);
        Assert.False(string.IsNullOrEmpty(cuerpo.RefreshToken));
        Assert.Equal(Correo, cuerpo.Usuario.Email);
        Assert.Equal(new[] { Roles.Gerente }, cuerpo.Usuario.Roles);

        var jwt = new JsonWebTokenHandler().ReadJsonWebToken(cuerpo.AccessToken);
        Assert.Equal(Roles.Gerente, jwt.GetClaim(TiposClaim.Rol).Value);
        Assert.Equal(TimeSpan.FromMinutes(30), jwt.ValidTo - jwt.IssuedAt);
    }

    // Contraseña incorrecta -> 401 con ProblemDetails y el mensaje genérico.
    [Fact]
    public async Task Login_PasswordIncorrecta_Devuelve401ConMensajeGenerico()
    {
        await _factory.CrearUsuarioAsync(Correo, Password, true, Roles.Mesero);

        var respuesta = await LoginAsync(Correo, PasswordIncorrecta);

        await AssertNoAutorizadoGenericoAsync(respuesta);
    }

    // Correo inexistente -> la misma respuesta que una contraseña incorrecta.
    [Fact]
    public async Task Login_CorreoInexistente_Devuelve401ConElMismoMensaje()
    {
        var respuesta = await LoginAsync("nadie@gira.local", Password);

        await AssertNoAutorizadoGenericoAsync(respuesta);
    }

    // 5 fallos consecutivos -> el sexto intento es rechazado aunque la contraseña sea correcta.
    [Fact]
    public async Task Login_CincoFallosConsecutivos_RechazaLaPasswordCorrecta()
    {
        await _factory.CrearUsuarioAsync(Correo, Password, true, Roles.Mesero);

        for (var intento = 0; intento < 5; intento++)
        {
            await LoginAsync(Correo, PasswordIncorrecta);
        }

        var respuesta = await LoginAsync(Correo, Password);

        await AssertNoAutorizadoGenericoAsync(respuesta);
    }

    // Usuario desactivado -> 401 aunque la contraseña sea correcta.
    [Fact]
    public async Task Login_UsuarioDesactivado_Devuelve401()
    {
        await _factory.CrearUsuarioAsync(Correo, Password, false, Roles.Mesero);

        var respuesta = await LoginAsync(Correo, Password);

        await AssertNoAutorizadoGenericoAsync(respuesta);
    }

    // Correo con formato inválido -> 400.
    [Fact]
    public async Task Login_CorreoConFormatoInvalido_Devuelve400()
    {
        var respuesta = await LoginAsync("no-es-un-correo", Password);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    // Más de 10 intentos en un minuto desde la misma IP -> 429.
    [Fact]
    public async Task Login_OnceIntentosEnUnMinuto_Devuelve429()
    {
        for (var intento = 0; intento < 10; intento++)
        {
            Assert.Equal(HttpStatusCode.Unauthorized, (await LoginAsync("nadie@gira.local", Password)).StatusCode);
        }

        var respuesta = await LoginAsync("nadie@gira.local", Password);

        Assert.Equal(HttpStatusCode.TooManyRequests, respuesta.StatusCode);
    }

    private static async Task AssertNoAutorizadoGenericoAsync(HttpResponseMessage respuesta)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        var problema = await respuesta.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal(AuthService.MensajeCredencialesInvalidas, problema!.Detail);
    }

    // GI-146: Cambiar contraseña requiere autenticación.
    [Fact]
    public async Task CambiarPassword_SinAutenticacion_Devuelve401()
    {
        var respuesta = await _cliente.PutAsJsonAsync(
            "/api/auth/cambiar-password",
            new CambiarPasswordRequest
            {
                PasswordActual = Password,
                PasswordNueva = "NuevaClave456!"
            });

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    // GI-146: Si la contraseña actual es incorrecta, devuelve 400.
    [Fact]
    public async Task CambiarPassword_PasswordIncorrecta_Devuelve400()
    {
        await _factory.CrearUsuarioAsync(
            Correo, Password, true, Roles.Mesero);

        var login = await LoginAsync(Correo, Password);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);

        var datos = await login.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.NotNull(datos);

        using var solicitud = new HttpRequestMessage(
            HttpMethod.Put,
            "/api/auth/cambiar-password");

        solicitud.Headers.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue(
                "Bearer", datos.AccessToken);

        solicitud.Content = JsonContent.Create(
            new CambiarPasswordRequest
            {
                PasswordActual = "Incorrecta123!",
                PasswordNueva = "NuevaClave456!"
            });

        var respuesta = await _cliente.SendAsync(solicitud);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);

        var problema = await respuesta.Content
            .ReadFromJsonAsync<ProblemDetails>();

        Assert.NotNull(problema);
    }

}
