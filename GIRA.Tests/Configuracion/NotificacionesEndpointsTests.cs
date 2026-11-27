using System.Net;
using System.Net.Http.Json;
using GIRA.Application.DTOs.Configuracion;
using GIRA.Application.Events.Inventario;
using GIRA.Application.Events.Personal;
using GIRA.Domain.Constants;
using GIRA.Domain.Enums.Inventario;
using GIRA.Infrastructure.Identity;
using GIRA.Tests.Api;
using MediatR;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Tests.Configuracion;

/// <summary>
/// De punta a punta (RF-CFG-05, RF-CFG-06): un evento real de otro módulo llega a la bandeja de quien corresponde, cada
/// quien ve y marca solo lo suyo, y el hub de SignalR acepta el token por la URL.
/// </summary>
public class NotificacionesEndpointsTests : IClassFixture<GiraApiFactory>
{
    private const string Password = "Prueba#Notif2026";
    private readonly GiraApiFactory _factory;

    public NotificacionesEndpointsTests(GiraApiFactory factory)
    {
        _factory = factory;
    }

    private async Task<HttpClient> ClienteAsync(string correo)
    {
        var cliente = _factory.CreateClient();
        cliente.UsarToken(await cliente.ObtenerTokenAsync(correo, Password));
        return cliente;
    }

    private async Task PublicarAsync<T>(T evento) where T : INotification
    {
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IPublisher>().Publish(evento);
    }

    [Fact]
    public async Task AlertaDeInventario_LlegaAlGerente_NoAlMesero_Y_SoloElDuenoLaMarcaLeida()
    {
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        var gerente = $"gerente-{sufijo}@gira.local";
        var mesero = $"mesero-{sufijo}@gira.local";
        await _factory.CrearUsuarioAsync(gerente, Password, true, Roles.Gerente);
        await _factory.CrearUsuarioAsync(mesero, Password, true, Roles.Mesero);

        await PublicarAsync(new AlertaInventarioGeneradaEvent(Guid.NewGuid(), Guid.NewGuid(), TipoAlertaInventario.StockBajo, 2m, null, null));

        var clienteGerente = await ClienteAsync(gerente);
        var bandeja = await clienteGerente.GetFromJsonAsync<BandejaNotificacionesDto>("/api/notificaciones/mias?leidas=false");
        var aviso = Assert.Single(bandeja!.Notificaciones);
        Assert.Equal(1, bandeja.NoLeidas);
        Assert.Equal("Stock bajo: un insumo", aviso.Titulo);

        var clienteMesero = await ClienteAsync(mesero);
        var delMesero = await clienteMesero.GetFromJsonAsync<BandejaNotificacionesDto>("/api/notificaciones/mias");
        Assert.Empty(delMesero!.Notificaciones);
        Assert.Equal(HttpStatusCode.NotFound, (await clienteMesero.PatchAsync($"/api/notificaciones/{aviso.Id}/leida", null)).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await clienteGerente.PatchAsync($"/api/notificaciones/{aviso.Id}/leida", null)).StatusCode);
        var despues = await clienteGerente.GetFromJsonAsync<BandejaNotificacionesDto>("/api/notificaciones/mias");
        Assert.Equal(0, despues!.NoLeidas);
        Assert.True(Assert.Single(despues.Notificaciones).Leida);
    }

    [Fact]
    public async Task TurnoAsignado_LlegaALaCuentaVinculadaAlEmpleado()
    {
        var correo = $"cocina-{Guid.NewGuid():N}@gira.local";
        var usuarioId = await _factory.CrearUsuarioAsync(correo, Password, true, Roles.Cocina);
        var empleadoId = Guid.NewGuid();
        using (var scope = _factory.Services.CreateScope())
        {
            var usuarios = scope.ServiceProvider.GetRequiredService<UserManager<Usuario>>();
            var usuario = await usuarios.FindByIdAsync(usuarioId.ToString());
            usuario!.EmpleadoId = empleadoId;
            await usuarios.UpdateAsync(usuario);
        }

        await PublicarAsync(new TurnoAsignadoEvent(Guid.NewGuid(), empleadoId, new DateOnly(2026, 12, 1), new TimeOnly(8, 0), new TimeOnly(16, 0)));

        var bandeja = await (await ClienteAsync(correo)).GetFromJsonAsync<BandejaNotificacionesDto>("/api/notificaciones/mias");
        Assert.Equal("Tienes turno el 01/12/2026 de 08:00 a 16:00.", Assert.Single(bandeja!.Notificaciones).Mensaje);
    }

    [Fact]
    public async Task SinToken_Bandeja_Y_Hub_Responden401_ConTokenEnLaUrl_ElHubAcepta()
    {
        var anonimo = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.GetAsync("/api/notificaciones/mias")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonimo.PostAsync("/hubs/notificaciones/negotiate?negotiateVersion=1", null)).StatusCode);

        var correo = $"supervisor-{Guid.NewGuid():N}@gira.local";
        await _factory.CrearUsuarioAsync(correo, Password, true, Roles.Supervisor);
        var token = await anonimo.ObtenerTokenAsync(correo, Password);
        var conToken = await anonimo.PostAsync($"/hubs/notificaciones/negotiate?negotiateVersion=1&access_token={token}", null);
        Assert.Equal(HttpStatusCode.OK, conToken.StatusCode);
    }
}
