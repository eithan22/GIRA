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
using System.Net;
using System.Net.Http.Json;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Pruebas HTTP de PATCH /api/reservas/{id}/check-in (GI-165, GI-201, GI-232): recorren el
/// pipeline real (JWT, política OperarReservas) con una base InMemory propia y un reloj
/// simulado para el servicio: martes 1 de diciembre de 2026, 12:00 hora de Santo Domingo.
/// </summary>
public class CheckInEndpointsTests : IAsyncLifetime
{
    private static readonly DateOnly Dia = new(2026, 12, 1);

    private readonly WebApplicationFactoryConReloj _factory = new();
    private HttpClient _cliente = null!;
    private Guid _reservaId;
    private Guid _reservaOtroDiaId;

    // La fábrica base crea la base InMemory; aquí solo se reemplaza el reloj del servicio.
    private sealed class WebApplicationFactoryConReloj : GiraApiFactory
    {
        protected override void ConfigureWebHost(Microsoft.AspNetCore.Hosting.IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(
                    new TimeProviderFalso(new DateTimeOffset(2026, 12, 1, 16, 0, 0, TimeSpan.Zero)));
            });
        }
    }

    public async Task InitializeAsync()
    {
        _cliente = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        var zona = new Zona { Id = Guid.NewGuid(), Nombre = "Zona Check-in" };
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 9101, Capacidad = 4, ZonaId = zona.Id };
        var franja = new FranjaHoraria
        {
            Id = Guid.NewGuid(), Nombre = "Franja Check-in", HoraInicio = new TimeOnly(12, 0), HoraFin = new TimeOnly(15, 0)
        };
        var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Cliente Check-in", Telefono = "8095550303" };
        var reserva = NuevaReserva("30303030", cliente, mesa, franja, Dia);
        var otroDia = NuevaReserva("30303031", cliente, mesa, franja, Dia.AddDays(1));
        contexto.Zonas.Add(zona);
        contexto.Mesas.Add(mesa);
        contexto.FranjasHorarias.Add(franja);
        contexto.Clientes.Add(cliente);
        contexto.Reservas.AddRange(reserva, otroDia);
        await contexto.SaveChangesAsync();

        _reservaId = reserva.Id;
        _reservaOtroDiaId = otroDia.Id;
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        await _factory.DisposeAsync();
    }

    private static Reserva NuevaReserva(string codigo, Cliente cliente, Mesa mesa, FranjaHoraria franja, DateOnly fecha) => new()
    {
        Id = Guid.NewGuid(), Codigo = codigo, ClienteId = cliente.Id, MesaId = mesa.Id,
        FranjaHorariaId = franja.Id, Fecha = fecha, Personas = 2, Origen = OrigenReserva.Telefono
    };

    private string GenerarToken(Guid usuarioId, params string[] roles)
    {
        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        return tokenService.GenerarAccessToken(usuarioId, "personal@gira.local", "Personal de Prueba", roles).Token;
    }

    private Task<HttpResponseMessage> CheckIn(Guid id, int personas) =>
        _cliente.PatchAsJsonAsync($"/api/reservas/{id}/check-in", new CheckInRequest(personas));

    [Fact]
    public async Task Sin_Token_Responde_401()
    {
        var respuesta = await CheckIn(_reservaId, 2);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Rol_Sin_OperarReservas_Responde_403_Y_No_Cambia_La_Reserva()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Cocina));

        var respuesta = await CheckIn(_reservaId, 2);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        Assert.Equal(EstadoReserva.Confirmada, (await contexto.Reservas.SingleAsync(r => r.Id == _reservaId)).Estado);
    }

    [Theory]
    [InlineData(Roles.Mesero)]
    [InlineData(Roles.Supervisor)]
    [InlineData(Roles.Gerente)]
    [InlineData(Roles.Administrador)]
    public async Task Con_Cada_Rol_Del_Personal_Responde_200_Y_Registra_El_Usuario(string rol)
    {
        var usuarioId = Guid.NewGuid();
        _cliente.UsarToken(GenerarToken(usuarioId, rol));

        var respuesta = await CheckIn(_reservaId, 3);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var detalle = await respuesta.Content.ReadFromJsonAsync<ReservaDetalleDto>();
        Assert.Equal("EnCurso", detalle!.Estado);
        Assert.Equal(3, detalle.PersonasReales);
        Assert.NotNull(detalle.FechaCheckIn);

        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        var guardada = await contexto.Reservas.SingleAsync(r => r.Id == _reservaId);
        Assert.Equal(EstadoReserva.EnCurso, guardada.Estado);
        Assert.Equal(usuarioId, guardada.UpdatedBy);
    }

    [Fact]
    public async Task Reserva_Inexistente_Responde_404()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));

        var respuesta = await CheckIn(Guid.NewGuid(), 2);

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Contains("No encontramos esa reserva.", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Mas_Personas_Que_La_Capacidad_Responde_400_Con_El_Mensaje()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));

        var respuesta = await CheckIn(_reservaId, 5);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("La mesa 9101 admite como máximo 4 personas.", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Antes_De_La_Ventana_Responde_409_Con_La_Fecha()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));

        var respuesta = await CheckIn(_reservaOtroDiaId, 2);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Contains("El check-in se habilita el 02/12 a partir de las 11:45.", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Segundo_CheckIn_Responde_409_Con_El_Mensaje_De_EnCurso()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));
        Assert.Equal(HttpStatusCode.OK, (await CheckIn(_reservaId, 2)).StatusCode);

        var segundo = await CheckIn(_reservaId, 2);

        Assert.Equal(HttpStatusCode.Conflict, segundo.StatusCode);
        Assert.Contains("Esta reserva ya tiene el check-in registrado.", await segundo.Content.ReadAsStringAsync());
    }
}
