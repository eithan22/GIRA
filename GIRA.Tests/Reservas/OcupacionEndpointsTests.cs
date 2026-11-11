using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Domain.Constants;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Tests.Api;
using Microsoft.Extensions.DependencyInjection;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Pruebas HTTP de GET /api/reservas/ocupacion (GI-167, GI-206): pipeline real (JWT, política OperarReservas),
/// mensajes de 400 y 404 en español, estado como texto, sin teléfono, y que la ruta literal no choca con
/// GET /api/reservas/{id:guid}. El reloj del servicio marca las 12:00 del 1 de diciembre de 2026 en Santo Domingo.
/// </summary>
public class OcupacionEndpointsTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = new(2026, 12, 1);

    private GiraApiFactoryConReloj _factory = null!;
    private HttpClient _cliente = null!;
    private Guid _franjaId;
    private Guid _reservaConfirmadaId;

    public async Task InitializeAsync()
    {
        _factory = new GiraApiFactoryConReloj(new DateTimeOffset(2026, 12, 1, 16, 0, 0, TimeSpan.Zero));
        _cliente = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        var zona = new Zona { Id = Guid.NewGuid(), Nombre = "Zona Mapa" };
        var libre = new Mesa { Id = Guid.NewGuid(), Numero = 9801, Capacidad = 2, ZonaId = zona.Id };
        var reservada = new Mesa { Id = Guid.NewGuid(), Numero = 9802, Capacidad = 4, ZonaId = zona.Id };
        var ocupada = new Mesa { Id = Guid.NewGuid(), Numero = 9803, Capacidad = 4, ZonaId = zona.Id };
        var inactiva = new Mesa { Id = Guid.NewGuid(), Numero = 9804, Capacidad = 4, ZonaId = zona.Id, Activa = false };
        var franja = new FranjaHoraria
        {
            Id = Guid.NewGuid(), Nombre = "Franja Mapa", HoraInicio = new TimeOnly(12, 0), HoraFin = new TimeOnly(15, 0)
        };
        var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Cliente Mapa", Telefono = "8095550808" };
        var confirmada = NuevaReserva("80808080", cliente, reservada, franja);
        var enCurso = NuevaReserva("80808081", cliente, ocupada, franja);
        enCurso.HacerCheckIn(3, DateTime.UtcNow);
        var enInactiva = NuevaReserva("80808082", cliente, inactiva, franja);
        contexto.Zonas.Add(zona);
        contexto.Mesas.AddRange(libre, reservada, ocupada, inactiva);
        contexto.FranjasHorarias.Add(franja);
        contexto.Clientes.Add(cliente);
        contexto.Reservas.AddRange(confirmada, enCurso, enInactiva);
        await contexto.SaveChangesAsync();

        _franjaId = franja.Id;
        _reservaConfirmadaId = confirmada.Id;
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        await _factory.DisposeAsync();
    }

    private static Reserva NuevaReserva(string codigo, Cliente cliente, Mesa mesa, FranjaHoraria franja) => new()
    {
        Id = Guid.NewGuid(), Codigo = codigo, ClienteId = cliente.Id, MesaId = mesa.Id,
        FranjaHorariaId = franja.Id, Fecha = Hoy, Personas = 2, Origen = OrigenReserva.Telefono
    };

    private string GenerarToken(params string[] roles)
    {
        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        return tokenService.GenerarAccessToken(Guid.NewGuid(), "personal@gira.local", "Personal de Prueba", roles).Token;
    }

    private string UrlMapa(string consulta) => $"/api/reservas/ocupacion?{consulta}";

    [Fact]
    public async Task Sin_Token_Responde_401()
    {
        var respuesta = await _cliente.GetAsync(UrlMapa($"franjaId={_franjaId}"));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Rol_Sin_OperarReservas_Responde_403()
    {
        _cliente.UsarToken(GenerarToken(Roles.Cocina));

        var respuesta = await _cliente.GetAsync(UrlMapa($"franjaId={_franjaId}"));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Mesero)]
    [InlineData(Roles.Supervisor)]
    [InlineData(Roles.Gerente)]
    [InlineData(Roles.Administrador)]
    public async Task Con_Cada_Rol_Del_Personal_Responde_200_Con_Los_Estados_Como_Texto(string rol)
    {
        _cliente.UsarToken(GenerarToken(rol));

        var respuesta = await _cliente.GetAsync(UrlMapa($"fecha=2026-12-01&franjaId={_franjaId}"));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var json = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains("\"estado\":\"Reservada\"", json);
        Assert.Contains("\"estado\":\"Ocupada\"", json);
        Assert.Contains("\"estado\":\"Libre\"", json);
        Assert.Contains("\"estado\":\"Inactiva\"", json);
        Assert.DoesNotContain("8095550808", json);
        Assert.DoesNotContain("elefono", json);

        var mapa = JsonSerializer.Deserialize<MapaOcupacionDto>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var mesas = mapa.Zonas.SelectMany(z => z.Mesas).Where(m => m.Numero >= 9800).ToDictionary(m => m.Numero);
        Assert.Equal("Libre", mesas[9801].Estado);
        Assert.Equal("Reservada", mesas[9802].Estado);
        Assert.Equal("Ocupada", mesas[9803].Estado);
        Assert.Equal(3, mesas[9803].PersonasReales);
        // La mesa inactiva con reserva vigente sigue Inactiva pero deja ver la reserva.
        Assert.Equal("Inactiva", mesas[9804].Estado);
        Assert.NotNull(mesas[9804].ReservaId);
        Assert.Equal("Cliente Mapa", mesas[9804].ClienteNombre);
        Assert.Equal(mapa.Zonas.Sum(z => z.Mesas.Count),
            mapa.Resumen.Libres + mapa.Resumen.Reservadas + mapa.Resumen.Ocupadas + mapa.Resumen.Inactivas);
    }

    [Fact]
    public async Task Sin_Fecha_Usa_Hoy_En_Hora_De_Santo_Domingo()
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var respuesta = await _cliente.GetAsync(UrlMapa($"franjaId={_franjaId}"));

        var mapa = await respuesta.Content.ReadFromJsonAsync<MapaOcupacionDto>();
        Assert.Equal(Hoy, mapa!.Fecha);
        Assert.Contains(mapa.Zonas.SelectMany(z => z.Mesas), m => m.Numero == 9802 && m.Estado == "Reservada");
    }

    [Theory]
    [InlineData("", "La franja es obligatoria.")]
    [InlineData("franjaId=no-es-un-guid", "La franja indicada no es válida.")]
    public async Task Una_Franja_Faltante_O_Invalida_Responde_400_En_Espanol(string consulta, string mensaje)
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var respuesta = await _cliente.GetAsync(UrlMapa(consulta));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains(mensaje, await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Una_Fecha_Invalida_Responde_400_En_Espanol_Y_No_El_Error_Generico_Del_Enlazado()
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var respuesta = await _cliente.GetAsync(UrlMapa($"fecha=mañana&franjaId={_franjaId}"));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains("La fecha no es válida; usa el formato aaaa-mm-dd.", cuerpo);
        Assert.DoesNotContain("is not valid", cuerpo);
    }

    [Fact]
    public async Task Una_Franja_Inexistente_Responde_404_En_Espanol()
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var respuesta = await _cliente.GetAsync(UrlMapa($"franjaId={Guid.NewGuid()}"));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Contains("No encontramos esa franja.", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task La_Ruta_Literal_No_Choca_Con_El_Detalle_De_Una_Reserva_Por_Id()
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var mapa = await _cliente.GetAsync(UrlMapa($"franjaId={_franjaId}"));
        var detalle = await _cliente.GetAsync($"/api/reservas/{_reservaConfirmadaId}");
        var sinFranja = await _cliente.GetAsync("/api/reservas/ocupacion");

        Assert.Equal(HttpStatusCode.OK, mapa.StatusCode);
        Assert.Equal(HttpStatusCode.OK, detalle.StatusCode);
        Assert.Equal("80808080", (await detalle.Content.ReadFromJsonAsync<ReservaDetalleDto>())!.Codigo);
        // Sin franja llega al mapa (mensaje propio), no al detalle con un id inválido.
        Assert.Equal(HttpStatusCode.BadRequest, sinFranja.StatusCode);
        Assert.Contains("La franja es obligatoria.", await sinFranja.Content.ReadAsStringAsync());
    }
}
