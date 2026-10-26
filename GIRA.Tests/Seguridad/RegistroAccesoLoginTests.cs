using GIRA.Application.DTOs.Seguridad;
using GIRA.Domain.Constants;
using GIRA.Domain.Entities.Seguridad;
using GIRA.Infrastructure.Identity;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Tests.Api;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace GIRA.Tests.Seguridad;

public class RegistroAccesoLoginTests : IAsyncLifetime
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

    private async Task<List<RegistroAcceso>> LeerRegistrosAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        return await db.RegistrosAcceso.AsNoTracking().ToListAsync();
    }

    private async Task<Guid> ObtenerUsuarioIdAsync(string correo)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        var usuario = await userManager.FindByEmailAsync(correo);
        return usuario!.Id;
    }

    // Correo que no existe -> un registro sin usuario, fallido, con motivo UsuarioNoExiste.
    [Fact]
    public async Task Login_CorreoInexistente_RegistraUsuarioNoExiste()
    {
        var respuesta = await LoginAsync("nadie@gira.local", Password);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        var registro = Assert.Single(await LeerRegistrosAsync());
        Assert.Null(registro.UsuarioId);
        Assert.Equal("nadie@gira.local", registro.CorreoIntentado);
        Assert.False(registro.Exitoso);
        Assert.Equal(MotivosAcceso.UsuarioNoExiste, registro.Motivo);
        Assert.False(string.IsNullOrEmpty(registro.Ip));
    }

    // Usuario desactivado -> registro con su Id, fallido, con motivo Inactivo.
    [Fact]
    public async Task Login_UsuarioInactivo_RegistraInactivo()
    {
        await _factory.CrearUsuarioAsync(Correo, Password, false, Roles.Mesero);
        var usuarioId = await ObtenerUsuarioIdAsync(Correo);

        var respuesta = await LoginAsync(Correo, Password);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        var registro = Assert.Single(await LeerRegistrosAsync());
        Assert.Equal(usuarioId, registro.UsuarioId);
        Assert.False(registro.Exitoso);
        Assert.Equal(MotivosAcceso.Inactivo, registro.Motivo);
    }

    // Contraseña incorrecta (primer fallo, sin bloqueo todavía) -> motivo PasswordIncorrecta.
    [Fact]
    public async Task Login_PasswordIncorrecta_RegistraPasswordIncorrecta()
    {
        await _factory.CrearUsuarioAsync(Correo, Password, true, Roles.Mesero);
        var usuarioId = await ObtenerUsuarioIdAsync(Correo);

        var respuesta = await LoginAsync(Correo, PasswordIncorrecta);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        var registro = Assert.Single(await LeerRegistrosAsync());
        Assert.Equal(usuarioId, registro.UsuarioId);
        Assert.False(registro.Exitoso);
        Assert.Equal(MotivosAcceso.PasswordIncorrecta, registro.Motivo);
    }

    // 5 fallos y un sexto intento con la contraseña correcta -> los 6 quedan registrados y
    // el intento bloqueado tiene motivo Bloqueado. No se depende del orden por fecha, que puede empatar.
    [Fact]
    public async Task Login_CuentaBloqueada_RegistraBloqueado()
    {
        await _factory.CrearUsuarioAsync(Correo, Password, true, Roles.Mesero);

        for (var intento = 0; intento < 5; intento++)
        {
            await LoginAsync(Correo, PasswordIncorrecta);
        }

        var respuesta = await LoginAsync(Correo, Password);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        var registros = await LeerRegistrosAsync();
        Assert.Equal(6, registros.Count);
        Assert.All(registros, r => Assert.False(r.Exitoso));
        Assert.Contains(registros, r => r.Motivo == MotivosAcceso.PasswordIncorrecta);
        Assert.Contains(registros, r => r.Motivo == MotivosAcceso.Bloqueado);
        Assert.DoesNotContain(registros, r => r.Motivo is null);
    }

    // Login correcto -> registro exitoso, con el Id del usuario y sin motivo.
    [Fact]
    public async Task Login_CredencialesValidas_RegistraExito()
    {
        await _factory.CrearUsuarioAsync(Correo, Password, true, Roles.Gerente);
        var usuarioId = await ObtenerUsuarioIdAsync(Correo);

        var respuesta = await LoginAsync(Correo, Password);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var registro = Assert.Single(await LeerRegistrosAsync());
        Assert.Equal(usuarioId, registro.UsuarioId);
        Assert.Equal(Correo, registro.CorreoIntentado);
        Assert.True(registro.Exitoso);
        Assert.Null(registro.Motivo);
        Assert.InRange(registro.Fecha, DateTime.UtcNow.AddMinutes(-1), DateTime.UtcNow.AddMinutes(1));
    }

    // Una petición rechazada por validación (correo con formato inválido) no llega a la auditoría.
    [Fact]
    public async Task Login_CorreoConFormatoInvalido_NoRegistraNada()
    {
        var respuesta = await LoginAsync("no-es-un-correo", Password);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Empty(await LeerRegistrosAsync());
    }
}
