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
/// Pruebas HTTP de GET /api/reservas/franjas (GI-399): pipeline real (JWT, política OperarReservas),
/// orden, inactivas y borradas, contrato mínimo, y que la ruta no choca con /{id:guid}, /disponibilidad
/// ni /ocupacion ni afloja GET /api/franjas (GestionarSalon). Las franjas de estas pruebas llevan el
/// prefijo "GI399" y los casos filtran por él, por si la base trae otras franjas.
/// </summary>
public class FranjasOperativasEndpointsTests : IAsyncLifetime
{
    private const string Url = "/api/reservas/franjas";
    private static readonly DateOnly Hoy = new(2026, 12, 1);

    private GiraApiFactoryConReloj _factory = null!;
    private HttpClient _cliente = null!;
    private Guid _reservaId;
    private Guid _franjaInactivaId;

    public async Task InitializeAsync()
    {
        _factory = new GiraApiFactoryConReloj(new DateTimeOffset(2026, 12, 1, 16, 0, 0, TimeSpan.Zero));
        _cliente = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();

        // Sembradas desordenadas a propósito: el orden debe venir de la consulta, no del alta.
        var tarde = new FranjaHoraria { Id = Guid.NewGuid(), Nombre = "GI399 Tarde", HoraInicio = new TimeOnly(16, 0), HoraFin = new TimeOnly(17, 0) };
        var manana = new FranjaHoraria { Id = Guid.NewGuid(), Nombre = "GI399 Mañana", HoraInicio = new TimeOnly(8, 0), HoraFin = new TimeOnly(9, 0) };
        var inactiva = new FranjaHoraria { Id = Guid.NewGuid(), Nombre = "GI399 Inactiva", HoraInicio = new TimeOnly(10, 0), HoraFin = new TimeOnly(11, 0), Activa = false };
        var borrada = new FranjaHoraria { Id = Guid.NewGuid(), Nombre = "GI399 Borrada", HoraInicio = new TimeOnly(13, 0), HoraFin = new TimeOnly(14, 0), IsDeleted = true };

        var zona = new Zona { Id = Guid.NewGuid(), Nombre = "Zona GI399" };
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 9399, Capacidad = 4, ZonaId = zona.Id };
        var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Cliente GI399", Telefono = "8095553990" };
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(), Codigo = "39939939", ClienteId = cliente.Id, MesaId = mesa.Id,
            FranjaHorariaId = tarde.Id, Fecha = Hoy, Personas = 2, Origen = OrigenReserva.Telefono
        };

        contexto.FranjasHorarias.AddRange(tarde, manana, inactiva, borrada);
        contexto.Zonas.Add(zona);
        contexto.Mesas.Add(mesa);
        contexto.Clientes.Add(cliente);
        contexto.Reservas.Add(reserva);
        await contexto.SaveChangesAsync();

        _reservaId = reserva.Id;
        _franjaInactivaId = inactiva.Id;
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

    private static readonly JsonSerializerOptions Web = new(JsonSerializerDefaults.Web);

    private async Task<List<FranjaOperativaDto>> ListarComoAsync(string rol)
    {
        _cliente.UsarToken(GenerarToken(rol));
        var respuesta = await _cliente.GetAsync(Url);
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        return (await respuesta.Content.ReadFromJsonAsync<List<FranjaOperativaDto>>(Web))!;
    }

    [Fact]
    public async Task Sin_Token_Responde_401()
    {
        var respuesta = await _cliente.GetAsync(Url);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Rol_Cocina_Responde_403()
    {
        _cliente.UsarToken(GenerarToken(Roles.Cocina));

        var respuesta = await _cliente.GetAsync(Url);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Mesero)]
    [InlineData(Roles.Supervisor)]
    [InlineData(Roles.Gerente)]
    [InlineData(Roles.Administrador)]
    public async Task Con_Cada_Rol_Del_Personal_Responde_200_Con_La_Lista(string rol)
    {
        var franjas = await ListarComoAsync(rol);

        Assert.Contains(franjas, f => f.Nombre == "GI399 Tarde");
    }

    [Fact]
    public async Task Viene_Ordenada_Por_Hora_De_Inicio()
    {
        var franjas = await ListarComoAsync(Roles.Mesero);

        Assert.Equal(franjas.OrderBy(f => f.HoraInicio).Select(f => f.Id), franjas.Select(f => f.Id));
        var propias = franjas.Where(f => f.Nombre.StartsWith("GI399")).Select(f => f.Nombre).ToList();
        Assert.Equal(new[] { "GI399 Mañana", "GI399 Inactiva", "GI399 Tarde" }, propias);
    }

    [Fact]
    public async Task Incluye_Las_Inactivas_Con_Activa_False()
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var respuesta = await _cliente.GetAsync(Url);

        var json = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains("\"activa\":false", json);
        var franjas = JsonSerializer.Deserialize<List<FranjaOperativaDto>>(json, Web)!;
        var inactiva = Assert.Single(franjas, f => f.Id == _franjaInactivaId);
        Assert.False(inactiva.Activa);
    }

    [Fact]
    public async Task Excluye_Las_Borradas()
    {
        var franjas = await ListarComoAsync(Roles.Mesero);

        Assert.DoesNotContain(franjas, f => f.Nombre == "GI399 Borrada");
    }

    [Fact]
    public async Task El_Json_Solo_Tiene_Los_Cinco_Campos_Sin_Datos_De_Administracion()
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var respuesta = await _cliente.GetAsync(Url);

        using var doc = JsonDocument.Parse(await respuesta.Content.ReadAsStringAsync());
        foreach (var franja in doc.RootElement.EnumerateArray())
        {
            Assert.Equal(
                new[] { "activa", "horaFin", "horaInicio", "id", "nombre" },
                franja.EnumerateObject().Select(p => p.Name).Order());
        }
    }

    [Fact]
    public async Task La_Ruta_Literal_No_Choca_Con_Detalle_Por_Id_Disponibilidad_Ni_Ocupacion()
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var franjas = await _cliente.GetAsync(Url);
        var detalle = await _cliente.GetAsync($"/api/reservas/{_reservaId}");
        var ocupacionSinFranja = await _cliente.GetAsync("/api/reservas/ocupacion");

        Assert.Equal(HttpStatusCode.OK, franjas.StatusCode);
        // La lista de franjas es un arreglo, no el detalle de una reserva.
        Assert.StartsWith("[", (await franjas.Content.ReadAsStringAsync()).TrimStart());
        // El detalle por id sigue resolviéndose en su propia ruta.
        Assert.Equal(HttpStatusCode.OK, detalle.StatusCode);
        Assert.Equal("39939939", (await detalle.Content.ReadFromJsonAsync<ReservaDetalleDto>())!.Codigo);
        // Ocupación sin franja llega a su propio mapa (400 con mensaje propio), no a franjas ni al detalle.
        Assert.Equal(HttpStatusCode.BadRequest, ocupacionSinFranja.StatusCode);
        Assert.Contains("La franja es obligatoria.", await ocupacionSinFranja.Content.ReadAsStringAsync());

        // Disponibilidad sigue siendo anónima: sin token no la bloquea la política de franjas.
        using var anonimo = _factory.CreateClient();
        var disponibilidad = await anonimo.GetAsync("/api/reservas/disponibilidad?fecha=2026-12-02&personas=2");
        Assert.Equal(HttpStatusCode.OK, disponibilidad.StatusCode);
    }

    [Fact]
    public async Task El_Listado_De_Administracion_Sigue_Dando_403_Para_Mesero()
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var respuesta = await _cliente.GetAsync("/api/franjas");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }
}
