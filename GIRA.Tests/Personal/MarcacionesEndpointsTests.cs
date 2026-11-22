using System.Net;
using System.Net.Http.Json;
using GIRA.Application.DTOs.Personal;
using GIRA.Domain.Constants;
using GIRA.Tests.Api;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Tests.Personal;

public sealed class MarcacionesEndpointsTests : IAsyncLifetime
{
    private readonly GiraApiFactory _factory = new();
    private HttpClient _cliente = null!;

    public async Task InitializeAsync()
    {
        var usuario = await _factory.CrearUsuarioAsync(
            $"{Guid.NewGuid():N}@gira.local", "Clave#Segura1", true, Roles.Mesero);
        _cliente = _factory.CreateClient();
        _cliente.UsarToken(Token(_factory, Roles.Mesero, usuario));
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task Registra_Entrada_Salida_Y_Expone_El_Turno_Propio()
    {
        var entrada = await _cliente.PostAsync("/api/personal/marcaciones/entrada", content: null);
        var turno = await entrada.Content.ReadFromJsonAsync<MarcacionTurnoDto>();

        Assert.Equal(HttpStatusCode.Created, entrada.StatusCode);
        Assert.Equal("/api/personal/marcaciones/actual", entrada.Headers.Location!.AbsolutePath);
        Assert.NotNull(turno);
        Assert.Equal(HttpStatusCode.OK,
            (await _cliente.GetAsync("/api/personal/marcaciones/actual")).StatusCode);

        var salida = await _cliente.PostAsJsonAsync(
            "/api/personal/marcaciones/salida", new FinalizarTurnoRequest { MinutosPausa = 0 });

        Assert.Equal(HttpStatusCode.OK, salida.StatusCode);
        Assert.NotNull((await salida.Content.ReadFromJsonAsync<MarcacionTurnoDto>())?.FinEn);
        Assert.Equal(HttpStatusCode.NotFound,
            (await _cliente.GetAsync("/api/personal/marcaciones/actual")).StatusCode);
    }

    [Fact]
    public async Task Rechaza_Dos_Entradas_Abiertas_Y_Llamadas_Sin_Autenticacion()
    {
        Assert.Equal(HttpStatusCode.Created,
            (await _cliente.PostAsync("/api/personal/marcaciones/entrada", content: null)).StatusCode);
        Assert.Equal(HttpStatusCode.Conflict,
            (await _cliente.PostAsync("/api/personal/marcaciones/entrada", content: null)).StatusCode);

        using var anonimo = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonimo.GetAsync("/api/personal/marcaciones/actual")).StatusCode);
    }

    private static string Token(GiraApiFactory factory, string rol, Guid usuarioId)
    {
        using var scope = factory.Services.CreateScope();
        return scope.ServiceProvider.GetRequiredService<GIRA.Application.Interfaces.Services.Seguridad.Auth.ITokenService>()
            .GenerarAccessToken(usuarioId, "personal@gira.local", "Personal de Prueba", [rol]).Token;
    }
}
