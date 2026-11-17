using System.Net;
using System.Net.Http.Json;
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
/// Pruebas HTTP de GET /api/reservas/codigo/{codigo} (GI-401): recorren el pipeline real (JWT, política
/// OperarReservas, límite por usuario) sobre una base InMemory propia por prueba. El código funciona sin depender
/// de la fecha ni del estado, y nunca se repite en los errores.
/// </summary>
public class BuscarPorCodigoEndpointsTests : IAsyncLifetime
{
    private const string CodigoFuturo = "90909090";
    private const string CodigoPasado = "00001234";

    private readonly GiraApiFactory _factory = new();
    private HttpClient _cliente = null!;
    private Guid _reservaFuturaId;

    public async Task InitializeAsync()
    {
        _cliente = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        var zona = new Zona { Id = Guid.NewGuid(), Nombre = "Zona Código" };
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 9401, Capacidad = 4, ZonaId = zona.Id };
        var franja = new FranjaHoraria
        {
            Id = Guid.NewGuid(), Nombre = "Franja Código", HoraInicio = new TimeOnly(12, 0), HoraFin = new TimeOnly(15, 0)
        };
        var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Cliente Código", Telefono = "8095550401" };
        var futura = NuevaReserva(CodigoFuturo, cliente, mesa, franja, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(20));
        var pasada = NuevaReserva(CodigoPasado, cliente, mesa, franja, DateOnly.FromDateTime(DateTime.UtcNow).AddDays(-90));
        pasada.Cancelar("El cliente viajó");
        contexto.Zonas.Add(zona);
        contexto.Mesas.Add(mesa);
        contexto.FranjasHorarias.Add(franja);
        contexto.Clientes.Add(cliente);
        contexto.Reservas.AddRange(futura, pasada);
        await contexto.SaveChangesAsync();
        _reservaFuturaId = futura.Id;
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        await _factory.DisposeAsync();
    }

    private static Reserva NuevaReserva(string codigo, Cliente cliente, Mesa mesa, FranjaHoraria franja, DateOnly fecha) => new()
    {
        Id = Guid.NewGuid(), Codigo = codigo, ClienteId = cliente.Id, MesaId = mesa.Id, FranjaHorariaId = franja.Id,
        Fecha = fecha, Personas = 3, Origen = OrigenReserva.Telefono
    };

    private string GenerarToken(params string[] roles)
    {
        using var scope = _factory.Services.CreateScope();
        var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
        return tokens.GenerarAccessToken(Guid.NewGuid(), "personal@gira.local", "Personal de Prueba", roles).Token;
    }

    private static string Ruta(string codigo) => $"/api/reservas/codigo/{Uri.EscapeDataString(codigo)}";

    [Fact]
    public async Task Sin_Token_Responde_401()
    {
        var respuesta = await _cliente.GetAsync(Ruta(CodigoFuturo));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Rol_Sin_OperarReservas_Responde_403()
    {
        _cliente.UsarToken(GenerarToken(Roles.Cocina));

        var respuesta = await _cliente.GetAsync(Ruta(CodigoFuturo));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
    }

    [Theory]
    [InlineData(Roles.Mesero)]
    [InlineData(Roles.Supervisor)]
    [InlineData(Roles.Gerente)]
    [InlineData(Roles.Administrador)]
    public async Task Con_Cada_Rol_Del_Personal_Responde_200_Con_El_Detalle(string rol)
    {
        _cliente.UsarToken(GenerarToken(rol));

        var respuesta = await _cliente.GetAsync(Ruta(CodigoFuturo));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var detalle = await respuesta.Content.ReadFromJsonAsync<ReservaDetalleDto>();
        Assert.Equal(_reservaFuturaId, detalle!.Id);
        Assert.Equal(CodigoFuturo, detalle.Codigo);
        Assert.Equal("Confirmada", detalle.Estado);
        Assert.Equal("Cliente Código", detalle.ClienteNombre);
        Assert.Equal(9401, detalle.MesaNumero);
    }

    [Fact]
    public async Task Responde_200_Sin_Depender_De_La_Fecha_Ni_Del_Estado()
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var futura = await _cliente.GetAsync(Ruta(CodigoFuturo));
        var pasada = await _cliente.GetAsync(Ruta(CodigoPasado));

        Assert.Equal(HttpStatusCode.OK, futura.StatusCode);
        Assert.Equal(HttpStatusCode.OK, pasada.StatusCode);
        var detalle = await pasada.Content.ReadFromJsonAsync<ReservaDetalleDto>();
        Assert.Equal("00001234", detalle!.Codigo);
        Assert.Equal("Cancelada", detalle.Estado);
        Assert.Equal("El cliente viajó", detalle.MotivoCancelacion);
    }

    [Theory]
    [InlineData("9090 9090")]
    [InlineData("9090-9090")]
    [InlineData(" 9090 - 9090 ")]
    public async Task Acepta_El_Codigo_Con_Espacios_O_Guiones(string escrito)
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var respuesta = await _cliente.GetAsync(Ruta(escrito));

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var detalle = await respuesta.Content.ReadFromJsonAsync<ReservaDetalleDto>();
        Assert.Equal(CodigoFuturo, detalle!.Codigo);
    }

    [Fact]
    public async Task Codigo_De_8_Digitos_Que_No_Existe_Responde_404_Con_El_Mensaje_De_La_Historia()
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var respuesta = await _cliente.GetAsync(Ruta("12345678"));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains("No encontramos esa reserva.", cuerpo);
        Assert.DoesNotContain("12345678", cuerpo);
    }

    [Theory]
    [InlineData("1234567")]
    [InlineData("123456789")]
    [InlineData("1234567A")]
    [InlineData("ABCDEFGH")]
    [InlineData("١٢٣٤٥٦٧٨")] // 8 dígitos árabe-índicos
    public async Task Formato_Que_No_Es_De_8_Digitos_Responde_400_Sin_Repetir_Lo_Escrito(string escrito)
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var respuesta = await _cliente.GetAsync(Ruta(escrito));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        var cuerpo = await respuesta.Content.ReadAsStringAsync();
        Assert.Contains("El código debe tener 8 dígitos.", cuerpo);
        Assert.DoesNotContain(escrito, cuerpo);
    }

    [Fact]
    public async Task Pasado_El_Limite_De_20_Por_Minuto_Y_Usuario_Responde_429()
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero)); // un solo usuario para todas las búsquedas

        for (var i = 0; i < 20; i++)
        {
            var permitida = await _cliente.GetAsync(Ruta("12345678"));
            Assert.Equal(HttpStatusCode.NotFound, permitida.StatusCode);
        }

        var excedida = await _cliente.GetAsync(Ruta("12345678"));

        Assert.Equal(HttpStatusCode.TooManyRequests, excedida.StatusCode);
    }

    [Fact]
    public async Task El_Limite_Es_Por_Usuario_Y_Otro_Usuario_No_Se_Ve_Afectado()
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero));
        for (var i = 0; i < 21; i++)
        {
            await _cliente.GetAsync(Ruta("12345678"));
        }

        _cliente.UsarToken(GenerarToken(Roles.Supervisor));
        var otroUsuario = await _cliente.GetAsync(Ruta(CodigoFuturo));

        Assert.Equal(HttpStatusCode.OK, otroUsuario.StatusCode);
    }

    [Fact]
    public async Task Regresion_El_Detalle_Por_Id_Sigue_Funcionando()
    {
        _cliente.UsarToken(GenerarToken(Roles.Mesero));

        var respuesta = await _cliente.GetAsync($"/api/reservas/{_reservaFuturaId}");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
    }

    [Fact]
    public async Task Regresion_La_Cancelacion_Publica_Por_Codigo_Sigue_Siendo_Anonima()
    {
        // Sin token y con un teléfono que no coincide: el mismo 404 neutro de siempre, nunca 401 ni 405.
        var respuesta = await _cliente.PatchAsJsonAsync($"/api/reservas/codigo/{CodigoFuturo}/cancelar", new { telefono = "8091119999" });

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
    }
}
