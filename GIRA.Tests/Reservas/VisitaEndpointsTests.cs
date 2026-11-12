using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Events.Reservas;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Domain.Constants;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Tests.Api;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Net;
using System.Net.Http.Json;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Pruebas HTTP de POST /api/visitas (GI-166, GI-204). El reloj del servicio marca las 13:00 del
/// 1 de diciembre de 2026 en Santo Domingo (Almuerzo ya empezó; Cena todavía no). Hay un handler que
/// anota el evento y otro que siempre falla: el walk-in responde 200 y queda guardado igualmente.
/// </summary>
public class VisitaEndpointsTests : IAsyncLifetime
{
    private static readonly DateOnly Dia = new(2026, 12, 1);

    private readonly RegistroEventos _registro = new();
    private GiraApiFactoryConReloj _factory = null!;
    private HttpClient _cliente = null!;
    private Guid _almuerzoId;
    private Guid _cenaId;
    private Guid _mesaLibreId;
    private Guid _mesaReservadaId;

    public async Task InitializeAsync()
    {
        _factory = new GiraApiFactoryConReloj(
            new DateTimeOffset(2026, 12, 1, 17, 0, 0, TimeSpan.Zero), // 13:00 locales
            servicios =>
            {
                servicios.AddSingleton(_registro);
                servicios.AddTransient<INotificationHandler<VisitaCompletadaEvent>, HandlerRegistrador>();
                servicios.AddTransient<INotificationHandler<VisitaCompletadaEvent>, HandlerQueFalla>();
            });
        _cliente = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        var zona = new Zona { Id = Guid.NewGuid(), Nombre = "Zona Visitas" };
        var libre = new Mesa { Id = Guid.NewGuid(), Numero = 9401, Capacidad = 4, ZonaId = zona.Id };
        var reservada = new Mesa { Id = Guid.NewGuid(), Numero = 9402, Capacidad = 4, ZonaId = zona.Id };
        var almuerzo = new FranjaHoraria
        {
            Id = Guid.NewGuid(), Nombre = "Almuerzo Visitas", HoraInicio = new TimeOnly(12, 0), HoraFin = new TimeOnly(15, 0)
        };
        var cena = new FranjaHoraria
        {
            Id = Guid.NewGuid(), Nombre = "Cena Visitas", HoraInicio = new TimeOnly(18, 0), HoraFin = new TimeOnly(22, 30)
        };
        var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Cliente Visitas", Telefono = "8095550505" };
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(), Codigo = "50505050", ClienteId = cliente.Id, MesaId = reservada.Id,
            FranjaHorariaId = almuerzo.Id, Fecha = Dia, Personas = 2, Origen = OrigenReserva.Telefono
        };
        contexto.Zonas.Add(zona);
        contexto.Mesas.AddRange(libre, reservada);
        contexto.FranjasHorarias.AddRange(almuerzo, cena);
        contexto.Clientes.Add(cliente);
        contexto.Reservas.Add(reserva);
        await contexto.SaveChangesAsync();

        _almuerzoId = almuerzo.Id;
        _cenaId = cena.Id;
        _mesaLibreId = libre.Id;
        _mesaReservadaId = reservada.Id;
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        await _factory.DisposeAsync();
    }

    private string GenerarToken(Guid usuarioId, params string[] roles)
    {
        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        return tokenService.GenerarAccessToken(usuarioId, "personal@gira.local", "Personal de Prueba", roles).Token;
    }

    private Task<HttpResponseMessage> Registrar(Guid franjaId, Guid? mesaId, int personas) =>
        _cliente.PostAsJsonAsync("/api/visitas", new RegistrarVisitaRequest(franjaId, mesaId, personas));

    private async Task<List<VisitaSinReserva>> LeerVisitasAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        return await contexto.VisitasSinReserva.AsNoTracking().ToListAsync();
    }

    [Fact]
    public async Task Sin_Token_Responde_401()
    {
        var respuesta = await Registrar(_almuerzoId, _mesaLibreId, 2);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Rol_Sin_OperarReservas_Responde_403_Y_No_Guarda_Ni_Publica()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Cocina));

        var respuesta = await Registrar(_almuerzoId, _mesaLibreId, 2);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Empty(await LeerVisitasAsync());
        Assert.Empty(_registro.Visitas);
    }

    [Theory]
    [InlineData(Roles.Mesero)]
    [InlineData(Roles.Supervisor)]
    [InlineData(Roles.Gerente)]
    [InlineData(Roles.Administrador)]
    public async Task Con_Cada_Rol_Responde_200_Registra_El_Usuario_Y_Publica_El_Evento_Una_Vez(string rol)
    {
        var usuarioId = Guid.NewGuid();
        _cliente.UsarToken(GenerarToken(usuarioId, rol));

        var respuesta = await Registrar(_almuerzoId, _mesaLibreId, 3);

        // Aunque HandlerQueFalla lanza, responde 200 y la visita queda guardada.
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var dto = await respuesta.Content.ReadFromJsonAsync<VisitaDto>();
        Assert.Equal(Dia, dto!.Fecha);
        Assert.Equal(9401, dto.MesaNumero);
        var visita = Assert.Single(await LeerVisitasAsync());
        Assert.Equal(usuarioId, visita.CreatedBy);
        Assert.Equal(3, visita.PersonasReales);
        var evento = Assert.Single(_registro.Visitas);
        Assert.Equal(new VisitaCompletadaEvent(Dia, _almuerzoId, 3, ConReserva: false), evento);
    }

    [Fact]
    public async Task Sin_Mesa_Responde_200_Con_MesaId_Nulo()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));

        var respuesta = await Registrar(_almuerzoId, null, 8);

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        Assert.Null(Assert.Single(await LeerVisitasAsync()).MesaId);
    }

    [Theory]
    [InlineData(0, "Debe haber al menos 1 persona.")]
    [InlineData(5, "La mesa 9401 admite como máximo 4 personas.")]
    public async Task Personas_Invalidas_Responden_400_Con_El_Mensaje(int personas, string mensaje)
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));

        var respuesta = await Registrar(_almuerzoId, _mesaLibreId, personas);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains(mensaje, await respuesta.Content.ReadAsStringAsync());
        Assert.Empty(_registro.Visitas);
    }

    [Fact]
    public async Task Franja_Inexistente_Responde_400()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));

        var respuesta = await Registrar(Guid.NewGuid(), null, 2);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("La franja indicada no existe.", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Franja_Futura_Responde_409_Con_Su_Hora_De_Inicio()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));

        var respuesta = await Registrar(_cenaId, null, 2);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Contains("La franja Cena Visitas empieza a las 18:00", await respuesta.Content.ReadAsStringAsync());
        Assert.Empty(await LeerVisitasAsync());
    }

    [Fact]
    public async Task Mesa_Con_Reserva_En_Esa_Franja_Responde_409()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));

        var respuesta = await Registrar(_almuerzoId, _mesaReservadaId, 2);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Contains("La mesa 9402 tiene una reserva en esta franja.", await respuesta.Content.ReadAsStringAsync());
        Assert.Empty(_registro.Visitas);
    }
}
