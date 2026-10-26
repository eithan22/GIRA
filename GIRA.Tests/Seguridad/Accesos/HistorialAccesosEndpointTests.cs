using GIRA.Application.DTOs.Seguridad.Accesos;
using GIRA.Domain.Constants;
using GIRA.Domain.Entities.Seguridad;
using GIRA.Infrastructure.Identity;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Tests.Api;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace GIRA.Tests.Seguridad;

public class HistorialAccesosEndpointTests : IAsyncLifetime
{
    private const string Password = "Clave-Segura1";
    private const string CorreoAdmin = "admin@gira.local";
    private const string CorreoGerente = "gerente@gira.local";
    private const string CorreoObjetivo = "ana.perez@gira.local";
    private const string CorreoOtro = "otro@gira.local";

    private static readonly DateTime Base = new(2026, 1, 10, 12, 0, 0, DateTimeKind.Utc);

    private readonly GiraApiFactory _factory = new();
    private HttpClient _cliente = null!;
    private Guid _objetivoId;
    private Guid _otroId;

    public async Task InitializeAsync()
    {
        _cliente = _factory.CreateClient();
        await _factory.CrearUsuarioAsync(CorreoAdmin, Password, true, Roles.Administrador);
        await _factory.CrearUsuarioAsync(CorreoGerente, Password, true, Roles.Gerente);
        await _factory.CrearUsuarioAsync(CorreoObjetivo, Password, true, Roles.Mesero);
        await _factory.CrearUsuarioAsync(CorreoOtro, Password, true, Roles.Mesero);
        _objetivoId = await ObtenerIdAsync(CorreoObjetivo);
        _otroId = await ObtenerIdAsync(CorreoOtro);
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        await _factory.DisposeAsync();
    }

    private async Task<Guid> ObtenerIdAsync(string correo)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
        return (await userManager.FindByEmailAsync(correo))!.Id;
    }

    /// <summary>Inserta registros del usuario a propósito desordenados; el día 1 es el más antiguo y el 5 el más reciente.</summary>
    private async Task SembrarAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GiraDbContext>();

        db.RegistrosAcceso.AddRange(
            Registro(_objetivoId, Base.AddDays(3), exitoso: true),
            Registro(_objetivoId, Base.AddDays(1), exitoso: false, motivo: MotivosAcceso.PasswordIncorrecta),
            Registro(_objetivoId, Base.AddDays(5), exitoso: true),
            Registro(_objetivoId, Base.AddDays(2), exitoso: false, motivo: MotivosAcceso.Bloqueado),
            Registro(_objetivoId, Base.AddDays(4), exitoso: false, motivo: MotivosAcceso.PasswordIncorrecta),
            Registro(_otroId, Base.AddDays(6), exitoso: true));

        await db.SaveChangesAsync();
    }

    private static RegistroAcceso Registro(Guid usuarioId, DateTime fecha, bool exitoso, string? motivo = null)
    {
        return new RegistroAcceso
        {
            Id = Guid.NewGuid(),
            UsuarioId = usuarioId,
            CorreoIntentado = "ana.perez@gira.local",
            Fecha = fecha,
            Ip = "127.0.0.1",
            Exitoso = exitoso,
            Motivo = motivo
        };
    }

    private async Task ComoAdministradorAsync()
    {
        _cliente.UsarToken(await _cliente.ObtenerTokenAsync(CorreoAdmin, Password));
    }

    private static string Fecha(DateTime fecha) => Uri.EscapeDataString(fecha.ToString("O"));

    [Fact]
    public async Task Accesos_SinToken_Devuelve401()
    {
        var respuesta = await _cliente.GetAsync($"/api/usuarios/{_objetivoId}/accesos");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Accesos_ComoGerente_Devuelve403()
    {
        _cliente.UsarToken(await _cliente.ObtenerTokenAsync(CorreoGerente, Password));

        var respuesta = await _cliente.GetAsync($"/api/usuarios/{_objetivoId}/accesos");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task Accesos_ComoAdministrador_DevuelveMasRecientePrimeroSinMezclarUsuarios()
    {
        await SembrarAsync();
        await ComoAdministradorAsync();

        var respuesta = await _cliente.GetAsync($"/api/usuarios/{_objetivoId}/accesos");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<HistorialAccesosDto>();
        Assert.NotNull(cuerpo);
        Assert.Equal(5, cuerpo.Total);
        Assert.Equal(1, cuerpo.Page);
        Assert.Equal(20, cuerpo.PageSize);
        Assert.Equal(5, cuerpo.Items.Count);
        Assert.All(cuerpo.Items, i => Assert.Equal(_objetivoId, i.UsuarioId));
        Assert.Equal(
            new[] { 5, 4, 3, 2, 1 }.Select(d => Base.AddDays(d)),
            cuerpo.Items.Select(i => i.Fecha));
    }

    [Fact]
    public async Task Accesos_Paginado_DevuelveLaPaginaPedida()
    {
        await SembrarAsync();
        await ComoAdministradorAsync();

        var respuesta = await _cliente.GetAsync($"/api/usuarios/{_objetivoId}/accesos?page=2&pageSize=2");

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<HistorialAccesosDto>();
        Assert.NotNull(cuerpo);
        Assert.Equal(5, cuerpo.Total);
        Assert.Equal(2, cuerpo.Page);
        Assert.Equal(2, cuerpo.PageSize);
        Assert.Equal(
            new[] { 3, 2 }.Select(d => Base.AddDays(d)),
            cuerpo.Items.Select(i => i.Fecha));
    }

    [Fact]
    public async Task Accesos_FiltroExitosoFalse_SoloDevuelveFallidos()
    {
        await SembrarAsync();
        await ComoAdministradorAsync();

        var respuesta = await _cliente.GetAsync($"/api/usuarios/{_objetivoId}/accesos?exitoso=false");

        var cuerpo = await respuesta.Content.ReadFromJsonAsync<HistorialAccesosDto>();
        Assert.NotNull(cuerpo);
        Assert.Equal(3, cuerpo.Total);
        Assert.All(cuerpo.Items, i => Assert.False(i.Exitoso));
    }

    [Fact]
    public async Task Accesos_FiltroPorRangoDeFechas_SoloDevuelveElRango()
    {
        await SembrarAsync();
        await ComoAdministradorAsync();

        var url = $"/api/usuarios/{_objetivoId}/accesos?desde={Fecha(Base.AddDays(2))}&hasta={Fecha(Base.AddDays(4))}";
        var respuesta = await _cliente.GetAsync(url);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadFromJsonAsync<HistorialAccesosDto>();
        Assert.NotNull(cuerpo);
        Assert.Equal(3, cuerpo.Total);
        Assert.Equal(
            new[] { 4, 3, 2 }.Select(d => Base.AddDays(d)),
            cuerpo.Items.Select(i => i.Fecha));
    }

    [Fact]
    public async Task Accesos_UsuarioInexistente_Devuelve404()
    {
        await ComoAdministradorAsync();

        var respuesta = await _cliente.GetAsync($"/api/usuarios/{Guid.NewGuid()}/accesos");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }

    [Theory]
    [InlineData("pageSize=0")]
    [InlineData("pageSize=101")]
    [InlineData("page=0")]
    public async Task Accesos_PaginacionInvalida_Devuelve400(string consulta)
    {
        await ComoAdministradorAsync();

        var respuesta = await _cliente.GetAsync($"/api/usuarios/{_objetivoId}/accesos?{consulta}");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Accesos_HastaAnteriorADesde_Devuelve400()
    {
        await ComoAdministradorAsync();

        var url = $"/api/usuarios/{_objetivoId}/accesos?desde={Fecha(Base.AddDays(4))}&hasta={Fecha(Base.AddDays(2))}";
        var respuesta = await _cliente.GetAsync(url);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }
}
