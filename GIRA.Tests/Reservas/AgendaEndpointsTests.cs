using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Domain.Constants;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Tests.Api;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Pruebas HTTP de la agenda del personal (GI-164): GET /api/reservas, GET /api/reservas/{id}
/// y GET /api/clientes, recorriendo el pipeline real (JWT, política OperarReservas) sobre una
/// base InMemory propia por prueba.
/// </summary>
public class AgendaEndpointsTests : IAsyncLifetime
{
    private static readonly DateOnly Fecha = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(3);

    private readonly GiraApiFactory _factory = new();
    private HttpClient _cliente = null!;
    private Guid _reservaId;
    private string _codigo = string.Empty;

    public async Task InitializeAsync()
    {
        _cliente = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        var zona = new Zona { Id = Guid.NewGuid(), Nombre = "Zona Agenda" };
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 9001, Capacidad = 4, ZonaId = zona.Id };
        var franja = new FranjaHoraria
        {
            Id = Guid.NewGuid(), Nombre = "Franja Agenda", HoraInicio = new TimeOnly(12, 0), HoraFin = new TimeOnly(15, 0)
        };
        var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Cliente Agenda", Telefono = "8095550909" };
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(), Codigo = "90909090", ClienteId = cliente.Id, MesaId = mesa.Id,
            FranjaHorariaId = franja.Id, Fecha = Fecha, Personas = 3, Origen = OrigenReserva.Telefono
        };
        contexto.Zonas.Add(zona);
        contexto.Mesas.Add(mesa);
        contexto.FranjasHorarias.Add(franja);
        contexto.Clientes.Add(cliente);
        contexto.Reservas.Add(reserva);
        await contexto.SaveChangesAsync();

        _reservaId = reserva.Id;
        _codigo = reserva.Codigo;
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

    private string UrlListado(string extra = "") => $"/api/reservas?fecha={Fecha:yyyy-MM-dd}{extra}";

    [Fact]
    public async Task Sin_Token_Los_Tres_Endpoints_Responden_401()
    {
        foreach (var url in new[] { "/api/reservas", $"/api/reservas/{_reservaId}", "/api/clientes?buscar=abc" })
        {
            var respuesta = await _cliente.GetAsync(url);

            Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
        }
    }

    [Fact]
    public async Task Rol_Sin_OperarReservas_Responde_403_En_Los_Tres_Endpoints()
    {
        _cliente.UsarToken(GenerarToken(Roles.Cocina));

        foreach (var url in new[] { "/api/reservas", $"/api/reservas/{_reservaId}", "/api/clientes?buscar=abc" })
        {
            var respuesta = await _cliente.GetAsync(url);

            Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        }
    }

    [Theory]
    [InlineData(Roles.Mesero)]
    [InlineData(Roles.Supervisor)]
    [InlineData(Roles.Gerente)]
    [InlineData(Roles.Administrador)]
    public async Task Listar_Con_Cada_Rol_Del_Personal_Responde_200_Con_El_Estado_Como_Texto(string rol)
    {
        _cliente.UsarToken(GenerarToken(rol));

        var respuesta = await _cliente.GetAsync(UrlListado("&estado=Confirmada"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var pagina = await respuesta.Content.ReadFromJsonAsync<PagedResult<ReservaListadoDto>>();
        var item = Assert.Single(pagina!.Items);
        Assert.Equal(_codigo, item.Codigo);
        Assert.Equal("Confirmada", item.Estado);
        Assert.Equal("Cliente Agenda", item.ClienteNombre);

        var json = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains("\"estado\":\"Confirmada\"", json);
    }

    [Fact]
    public async Task Listar_Por_Telefono_Con_Formato_Invalido_Responde_400()
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var respuesta = await _cliente.GetAsync("/api/reservas?telefono=12345");

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("El teléfono debe tener 10 dígitos", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Listar_Con_Page_Size_Mayor_A_100_Responde_400()
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var respuesta = await _cliente.GetAsync(UrlListado("&pageSize=101"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
    }

    [Fact]
    public async Task Obtener_Existente_Responde_200_Con_El_Detalle()
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var respuesta = await _cliente.GetAsync($"/api/reservas/{_reservaId}");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var detalle = await respuesta.Content.ReadFromJsonAsync<ReservaDetalleDto>();
        Assert.Equal(_codigo, detalle!.Codigo);
        Assert.Equal(9001, detalle.MesaNumero);
        Assert.Equal("Zona Agenda", detalle.ZonaNombre);
        Assert.Null(detalle.MotivoCancelacion);
    }

    [Fact]
    public async Task Obtener_Inexistente_Responde_404_Con_Mensaje_En_Espanol()
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var respuesta = await _cliente.GetAsync($"/api/reservas/{Guid.NewGuid()}");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Contains("No encontramos esa reserva.", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Buscar_Clientes_Responde_200_Y_Con_Menos_De_3_Caracteres_400()
    {
        _cliente.UsarToken(GenerarToken(Roles.Supervisor));

        var ok = await _cliente.GetAsync("/api/clientes?buscar=agenda");
        var corto = await _cliente.GetAsync("/api/clientes?buscar=ab");

        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var pagina = await ok.Content.ReadFromJsonAsync<PagedResult<ClienteDto>>();
        Assert.Equal("8095550909", Assert.Single(pagina!.Items).Telefono);
        Assert.Equal(HttpStatusCode.BadRequest, corto.StatusCode);
    }
}
