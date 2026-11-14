using System.Net;
using System.Net.Http.Json;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Domain.Constants;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Tests.Api;
using GIRA.Tests.Common;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Pruebas HTTP de los canales de reserva (GI-403) con un reloj simulado: martes 1 de diciembre de 2026,
/// 12:00 hora de Santo Domingo, con la franja de 12:00 a 15:00 ya en curso. Comprueban que el personal
/// puede ver y reservar la franja de hoy, y que el flujo público (anónimo) conserva la antelación de 2 h.
/// </summary>
public class CanalesReservaEndpointsTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = new(2026, 12, 1);

    private readonly FabricaConReloj _factory = new();
    private HttpClient _cliente = null!;
    private Guid _franjaId;

    private sealed class FabricaConReloj : GiraApiFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(
                    new TimeProviderFalso(new DateTimeOffset(2026, 12, 1, 16, 0, 0, TimeSpan.Zero))); // 12:00 RD
            });
        }
    }

    public async Task InitializeAsync()
    {
        _cliente = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        var zona = new Zona { Id = Guid.NewGuid(), Nombre = "Zona Canales" };
        var franja = new FranjaHoraria
        {
            Id = Guid.NewGuid(), Nombre = "Almuerzo Canales", HoraInicio = new TimeOnly(12, 0), HoraFin = new TimeOnly(15, 0)
        };
        contexto.Zonas.Add(zona);
        contexto.Mesas.Add(new Mesa { Id = Guid.NewGuid(), Numero = 9201, Capacidad = 4, ZonaId = zona.Id });
        contexto.FranjasHorarias.Add(franja);
        await contexto.SaveChangesAsync();
        _franjaId = franja.Id;
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        await _factory.DisposeAsync();
    }

    private string GenerarToken(params string[] roles)
    {
        using var scope = _factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        return tokens.GenerarAccessToken(Guid.NewGuid(), "personal@gira.local", "Personal de Prueba", roles).Token;
    }

    private static string UrlPersonal => $"/api/reservas/disponibilidad-personal?fecha={Hoy:yyyy-MM-dd}&personas=2";

    private static string UrlPublica => $"/api/reservas/disponibilidad?fecha={Hoy:yyyy-MM-dd}&personas=2";

    [Fact]
    public async Task DisponibilidadPersonal_SinToken_Devuelve401()
    {
        var respuesta = await _cliente.GetAsync(UrlPersonal);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task DisponibilidadPersonal_SinRol_Devuelve403()
    {
        _cliente.UsarToken(GenerarToken(Roles.Cocina));

        var respuesta = await _cliente.GetAsync(UrlPersonal);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Fact]
    public async Task DisponibilidadPersonal_ConRol_IncluyeLaFranjaDeHoyEnCurso()
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var respuesta = await _cliente.GetAsync(UrlPersonal);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var franjas = await respuesta.Content.ReadFromJsonAsync<List<FranjaDisponibleDto>>();
        Assert.Contains(franjas!, f => f.Id == _franjaId);
    }

    [Fact]
    public async Task DisponibilidadPublica_Anonima_ListaLaFranjaDeHoyPeroDeshabilitada()
    {
        // GI-226: el cliente recibe todas las franjas; la de hoy en curso sale sin poder elegirse y con su motivo.
        var respuesta = await _cliente.GetAsync(UrlPublica);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var franjas = await respuesta.Content.ReadFromJsonAsync<List<FranjaDisponibleDto>>();
        var franja = Assert.Single(franjas!, f => f.Id == _franjaId);
        Assert.False(franja.Disponible);
        Assert.Equal("Requiere reservar con al menos 2 horas de antelación.", franja.MotivoNoDisponible);
    }

    [Fact]
    public async Task DisponibilidadPublica_FechaPasada_Responde200ConFranjasQueYaPasaron_Y_LaDelPersonalSigueEn400()
    {
        var ayer = Hoy.AddDays(-1).ToString("yyyy-MM-dd");
        var urlAyer = $"/api/reservas/disponibilidad?fecha={ayer}&personas=2";

        var cliente = await _cliente.GetAsync(urlAyer);
        _cliente.UsarToken(GenerarToken(Roles.Mesero));
        var personal = await _cliente.GetAsync($"/api/reservas/disponibilidad-personal?fecha={ayer}&personas=2");

        Assert.Equal(HttpStatusCode.OK, cliente.StatusCode);
        var franjas = await cliente.Content.ReadFromJsonAsync<List<FranjaDisponibleDto>>();
        Assert.All(franjas!, f =>
        {
            Assert.False(f.Disponible);
            Assert.Equal("Esta franja ya pasó.", f.MotivoNoDisponible);
        });
        Assert.Equal(HttpStatusCode.BadRequest, personal.StatusCode);
        Assert.Contains("La fecha no puede ser en el pasado.", await personal.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DisponibilidadPublica_FueraDeLaVentana_Responde200ConElMotivo_Y_LaDelPersonalSigueEn400()
    {
        var lejos = Hoy.AddDays(45).ToString("yyyy-MM-dd");

        var cliente = await _cliente.GetAsync($"/api/reservas/disponibilidad?fecha={lejos}&personas=2");
        _cliente.UsarToken(GenerarToken(Roles.Mesero));
        var personal = await _cliente.GetAsync($"/api/reservas/disponibilidad-personal?fecha={lejos}&personas=2");

        Assert.Equal(HttpStatusCode.OK, cliente.StatusCode);
        var franjas = await cliente.Content.ReadFromJsonAsync<List<FranjaDisponibleDto>>();
        Assert.All(franjas!, f => Assert.Equal("Fuera del rango de reserva (máximo 30 días).", f.MotivoNoDisponible));
        Assert.Equal(HttpStatusCode.BadRequest, personal.StatusCode);
        Assert.Contains("La fecha supera la ventana máxima de anticipación.", await personal.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task DisponibilidadPublica_ConTokenDelPersonal_TampocoAbreLaReglaAlEndpointAnonimo()
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var respuesta = await _cliente.GetAsync(UrlPublica);

        var franjas = await respuesta.Content.ReadFromJsonAsync<List<FranjaDisponibleDto>>();
        Assert.False(Assert.Single(franjas!, f => f.Id == _franjaId).Disponible);
    }

    [Fact]
    public async Task Crear_Personal_ParaLaFranjaDeHoyEnCurso_Devuelve200()
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero));
        var request = new CrearReservaRequest("Cliente Canales", "8091120001", Hoy, _franjaId, 2, OrigenReserva.Presencial);

        var respuesta = await _cliente.PostAsJsonAsync("/api/reservas", request);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Crear_AnonimoConOrigenTelefonoEnElCuerpo_SigueExigiendoLas2hYNoCreaNada()
    {
        // Sin Authorization: invitado (Web); el Origen del cuerpo se ignora y rige la antelación de 2 h.
        var request = new CrearReservaRequest("Cliente Canales", "8091120002", Hoy, _franjaId, 2, OrigenReserva.Telefono);

        var respuesta = await _cliente.PostAsJsonAsync("/api/reservas", request);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        Assert.Equal(0, await contexto.Reservas.CountAsync());
    }

    [Fact]
    public async Task Crear_AnonimoConAntelacionSuficiente_CreaComoWebAunqueElCuerpoDigaTelefono()
    {
        var manana = Hoy.AddDays(1);
        var request = new CrearReservaRequest("Cliente Canales", "8091120003", manana, _franjaId, 2, OrigenReserva.Telefono);

        var respuesta = await _cliente.PostAsJsonAsync("/api/reservas", request);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var dto = await respuesta.Content.ReadFromJsonAsync<ReservaCreadaDto>();
        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        var reserva = await contexto.Reservas.FirstAsync(r => r.Codigo == dto!.Codigo);
        Assert.Equal(OrigenReserva.Web, reserva.Origen);
    }
}
