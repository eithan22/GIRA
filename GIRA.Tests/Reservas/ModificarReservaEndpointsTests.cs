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
/// Pruebas HTTP de PUT /api/reservas/{id} (GI-160, GI-187). El reloj del servicio marca las 10:00 del
/// 1 de diciembre de 2026 en Santo Domingo. La reserva se modifica para el 5 de diciembre; hay otra
/// de hoy en una franja que ya empezó. Handlers de prueba: uno anota el evento y otro siempre falla.
/// </summary>
public class ModificarReservaEndpointsTests : IAsyncLifetime
{
    private static readonly DateOnly Hoy = new(2026, 12, 1);
    private static readonly DateOnly Futuro = new(2026, 12, 5);

    private readonly RegistroEventos _registro = new();
    private GiraApiFactoryConReloj _factory = null!;
    private HttpClient _cliente = null!;
    private Guid _franjaId;
    private Guid _mesa4Id;
    private Guid _mesa6Id;
    private Guid _reservaId;
    private Guid _enCursoId;
    private Guid _yaEmpezoId;

    public async Task InitializeAsync()
    {
        _factory = new GiraApiFactoryConReloj(
            new DateTimeOffset(2026, 12, 1, 14, 0, 0, TimeSpan.Zero), // 10:00 locales
            servicios =>
            {
                servicios.AddSingleton(_registro);
                // Primero el que anota, luego el que falla: ForeachAwaitPublisher llega a ambos.
                servicios.AddTransient<INotificationHandler<ReservaModificadaEvent>, HandlerModificacionRegistrador>();
                servicios.AddTransient<INotificationHandler<ReservaModificadaEvent>, HandlerModificacionQueFalla>();
            });
        _cliente = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        var zona = new Zona { Id = Guid.NewGuid(), Nombre = "Zona Modificar" };
        var mesa4 = new Mesa { Id = Guid.NewGuid(), Numero = 9701, Capacidad = 4, ZonaId = zona.Id };
        var mesa6 = new Mesa { Id = Guid.NewGuid(), Numero = 9702, Capacidad = 6, ZonaId = zona.Id };
        var almuerzo = new FranjaHoraria
        {
            Id = Guid.NewGuid(), Nombre = "Almuerzo Modificar", HoraInicio = new TimeOnly(12, 0), HoraFin = new TimeOnly(15, 0)
        };
        var temprana = new FranjaHoraria
        {
            Id = Guid.NewGuid(), Nombre = "Temprana Modificar", HoraInicio = new TimeOnly(9, 0), HoraFin = new TimeOnly(11, 0)
        };
        var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Cliente Modificar", Telefono = "8095550707" };
        var reserva = NuevaReserva("70707070", cliente, mesa4, almuerzo, Futuro);
        var enCurso = NuevaReserva("70707071", cliente, mesa6, almuerzo, Futuro);
        enCurso.HacerCheckIn(2, DateTime.UtcNow);
        var yaEmpezo = NuevaReserva("70707072", cliente, mesa4, temprana, Hoy); // 09:00 de hoy: ya empezó a las 10:00
        contexto.Zonas.Add(zona);
        contexto.Mesas.AddRange(mesa4, mesa6);
        contexto.FranjasHorarias.AddRange(almuerzo, temprana);
        contexto.Clientes.Add(cliente);
        contexto.Reservas.AddRange(reserva, enCurso, yaEmpezo);
        await contexto.SaveChangesAsync();

        _franjaId = almuerzo.Id;
        _mesa4Id = mesa4.Id;
        _mesa6Id = mesa6.Id;
        _reservaId = reserva.Id;
        _enCursoId = enCurso.Id;
        _yaEmpezoId = yaEmpezo.Id;
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

    private Task<HttpResponseMessage> Modificar(Guid id, ModificarReservaRequest request) =>
        _cliente.PutAsJsonAsync($"/api/reservas/{id}", request);

    private async Task<Reserva> LeerAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        return await contexto.Reservas.AsNoTracking().SingleAsync(r => r.Id == id);
    }

    [Fact]
    public async Task Sin_Token_Responde_401()
    {
        var respuesta = await Modificar(_reservaId, new ModificarReservaRequest(Futuro, _franjaId, 3));

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Rol_Sin_OperarReservas_Responde_403_Sin_Cambiar_La_Reserva_Ni_Publicar()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Cocina));

        var respuesta = await Modificar(_reservaId, new ModificarReservaRequest(Futuro, _franjaId, 3));

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal(2, (await LeerAsync(_reservaId)).Personas);
        Assert.Empty(_registro.Modificaciones);
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

        var respuesta = await Modificar(_reservaId, new ModificarReservaRequest(Futuro, _franjaId, 4));

        // Aunque HandlerModificacionQueFalla lanza, responde 200 y queda guardada.
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var detalle = await respuesta.Content.ReadFromJsonAsync<ReservaDetalleDto>();
        Assert.Equal(4, detalle!.Personas);
        Assert.Equal(_mesa4Id, detalle.MesaId); // 4 personas siguen cabiendo en su mesa: no choca consigo misma
        var guardada = await LeerAsync(_reservaId);
        Assert.Equal(4, guardada.Personas);
        Assert.Equal(usuarioId, guardada.UpdatedBy);
        var evento = Assert.Single(_registro.Modificaciones);
        Assert.Equal(new ReservaModificadaEvent(_reservaId, Futuro, _franjaId, Futuro, _franjaId, 4), evento);
    }

    [Fact]
    public async Task Datos_Invalidos_Responden_400()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));

        var respuesta = await Modificar(_reservaId, new ModificarReservaRequest(Futuro, _franjaId, 0));

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("Debe haber al menos 1 persona.", await respuesta.Content.ReadAsStringAsync());
        Assert.Empty(_registro.Modificaciones);
    }

    [Fact]
    public async Task Reserva_Inexistente_Responde_404()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));

        var respuesta = await Modificar(Guid.NewGuid(), new ModificarReservaRequest(Futuro, _franjaId, 3));

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Contains("No encontramos esa reserva.", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Reserva_EnCurso_Responde_409_Con_El_Mensaje_Del_Estado()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));

        var respuesta = await Modificar(_enCursoId, new ModificarReservaRequest(Futuro, _franjaId, 3));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Contains("Esta reserva ya tiene el check-in registrado; no se puede modificar.", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Franja_Original_Que_Ya_Empezo_Responde_409()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));

        var respuesta = await Modificar(_yaEmpezoId, new ModificarReservaRequest(Hoy, _franjaId, 3));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Contains("La franja de esta reserva ya empezó; no se puede modificar.", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Si_No_Queda_Mesa_Libre_Que_Alcance_Responde_409()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));

        // 5 personas solo caben en la mesa de 6, tomada por la reserva EnCurso.
        var respuesta = await Modificar(_reservaId, new ModificarReservaRequest(Futuro, _franjaId, 5));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Contains("Esa mesa ya fue tomada, intenta otra franja.", await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(2, (await LeerAsync(_reservaId)).Personas);
    }

    [Fact]
    public async Task Mesa_Elegida_Ocupada_Responde_409_Y_No_Publica()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));

        // La mesa de 6 está tomada por la reserva EnCurso (mismo día y franja).
        var respuesta = await Modificar(_reservaId, new ModificarReservaRequest(Futuro, _franjaId, 3, _mesa6Id));

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Contains("La mesa 9702 ya está reservada en esa fecha y franja.", await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(_mesa4Id, (await LeerAsync(_reservaId)).MesaId);
        Assert.Empty(_registro.Modificaciones);
    }
}
