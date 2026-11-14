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
/// Pruebas HTTP de PATCH /api/reservas/{id}/no-asistio (GI-404). Recorren el pipeline real (JWT, política
/// OperarReservas, MediatR) con un reloj simulado: martes 1 de diciembre de 2026, 12:00 hora de Santo
/// Domingo. Hay un handler que anota el evento y otro que siempre falla, para comprobar que un handler que
/// falla no cambia la respuesta ni revierte la reserva.
/// </summary>
public class NoAsistioEndpointsTests : IAsyncLifetime
{
    private static readonly DateOnly Dia = new(2026, 12, 1);

    private readonly RegistroEventos _registro = new();
    private GiraApiFactoryConReloj _factory = null!;
    private HttpClient _cliente = null!;
    private Guid _franjaId;
    private Guid _confirmadaId;
    private Guid _futuraId;
    private Guid _enCursoId;

    public async Task InitializeAsync()
    {
        _factory = new GiraApiFactoryConReloj(
            new DateTimeOffset(2026, 12, 1, 16, 0, 0, TimeSpan.Zero),
            servicios =>
            {
                servicios.AddSingleton(_registro);
                // Primero el que anota, luego el que falla: ForeachAwaitPublisher llega a ambos.
                servicios.AddTransient<INotificationHandler<ReservaNoAsistidaEvent>, HandlerNoAsistidaRegistrador>();
                servicios.AddTransient<INotificationHandler<ReservaNoAsistidaEvent>, HandlerNoAsistidaQueFalla>();
            });
        _cliente = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        var zona = new Zona { Id = Guid.NewGuid(), Nombre = "Zona No asistió" };
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 9401, Capacidad = 4, ZonaId = zona.Id };
        var franja = new FranjaHoraria
        {
            Id = Guid.NewGuid(), Nombre = "Franja No asistió", HoraInicio = new TimeOnly(12, 0), HoraFin = new TimeOnly(15, 0)
        };
        var franjaTarde = new FranjaHoraria
        {
            Id = Guid.NewGuid(), Nombre = "Franja Tarde No asistió", HoraInicio = new TimeOnly(18, 0), HoraFin = new TimeOnly(20, 0)
        };
        var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Cliente No asistió", Telefono = "8095550505" };
        var confirmada = NuevaReserva("50505050", cliente, mesa, franja);
        var futura = NuevaReserva("50505051", cliente, mesa, franjaTarde);
        var enCurso = NuevaReserva("50505052", cliente, mesa, franja);
        enCurso.HacerCheckIn(2, DateTime.UtcNow);
        contexto.Zonas.Add(zona);
        contexto.Mesas.Add(mesa);
        contexto.FranjasHorarias.AddRange(franja, franjaTarde);
        contexto.Clientes.Add(cliente);
        contexto.Reservas.AddRange(confirmada, futura, enCurso);
        await contexto.SaveChangesAsync();

        _franjaId = franja.Id;
        _confirmadaId = confirmada.Id;
        _futuraId = futura.Id;
        _enCursoId = enCurso.Id;
    }

    public async Task DisposeAsync()
    {
        _cliente.Dispose();
        await _factory.DisposeAsync();
    }

    private static Reserva NuevaReserva(string codigo, Cliente cliente, Mesa mesa, FranjaHoraria franja) => new()
    {
        Id = Guid.NewGuid(), Codigo = codigo, ClienteId = cliente.Id, MesaId = mesa.Id,
        FranjaHorariaId = franja.Id, Fecha = Dia, Personas = 2, Origen = OrigenReserva.Telefono
    };

    private string GenerarToken(Guid usuarioId, params string[] roles)
    {
        using var scope = _factory.Services.CreateScope();
        var tokenService = scope.ServiceProvider.GetRequiredService<ITokenService>();
        return tokenService.GenerarAccessToken(usuarioId, "personal@gira.local", "Personal de Prueba", roles).Token;
    }

    private Task<HttpResponseMessage> MarcarNoAsistio(Guid id) => _cliente.PatchAsync($"/api/reservas/{id}/no-asistio", null);

    private async Task<Reserva> LeerAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        return await contexto.Reservas.AsNoTracking().SingleAsync(r => r.Id == id);
    }

    [Fact]
    public async Task Sin_Token_Responde_401()
    {
        var respuesta = await MarcarNoAsistio(_confirmadaId);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Rol_Sin_OperarReservas_Responde_403_Sin_Cambiar_La_Reserva_Ni_Publicar()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Cocina));

        var respuesta = await MarcarNoAsistio(_confirmadaId);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal(EstadoReserva.Confirmada, (await LeerAsync(_confirmadaId)).Estado);
        Assert.Empty(_registro.NoAsistidas);
    }

    [Theory]
    [InlineData(Roles.Mesero)]
    [InlineData(Roles.Supervisor)]
    [InlineData(Roles.Gerente)]
    [InlineData(Roles.Administrador)]
    public async Task Con_Cada_Rol_Responde_200_Marca_La_Reserva_Y_Publica_El_Evento_Una_Vez(string rol)
    {
        var usuarioId = Guid.NewGuid();
        _cliente.UsarToken(GenerarToken(usuarioId, rol));

        var respuesta = await MarcarNoAsistio(_confirmadaId);

        // Aunque HandlerNoAsistidaQueFalla lanza, la operación responde 200 y queda guardada.
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var detalle = await respuesta.Content.ReadFromJsonAsync<ReservaDetalleDto>();
        Assert.Equal("NoAsistio", detalle!.Estado);
        var guardada = await LeerAsync(_confirmadaId);
        Assert.Equal(EstadoReserva.NoAsistio, guardada.Estado);
        Assert.Equal(usuarioId, guardada.UpdatedBy);
        var evento = Assert.Single(_registro.NoAsistidas);
        Assert.Equal(new ReservaNoAsistidaEvent(_confirmadaId, Dia, _franjaId, 2), evento);
    }

    [Fact]
    public async Task Franja_Que_Aun_No_Empieza_Responde_409_Que_Remite_A_Cancelar_Y_No_Cambia_Nada()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));

        var respuesta = await MarcarNoAsistio(_futuraId); // franja de las 18:00; ahora son las 12:00

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Contains("La franja aún no empieza; si el cliente no vendrá, cancela la reserva.",
            await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(EstadoReserva.Confirmada, (await LeerAsync(_futuraId)).Estado);
        Assert.Empty(_registro.NoAsistidas);
    }

    [Fact]
    public async Task Reserva_EnCurso_Responde_409_Con_El_Mensaje_Del_Estado()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));

        var respuesta = await MarcarNoAsistio(_enCursoId);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Contains("Esta reserva ya tiene el check-in registrado; no se puede marcar como no asistida.",
            await respuesta.Content.ReadAsStringAsync());
        Assert.Empty(_registro.NoAsistidas);
    }

    [Fact]
    public async Task Un_Segundo_Intento_Responde_409_Y_No_Vuelve_A_Publicar()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));
        Assert.Equal(HttpStatusCode.OK, (await MarcarNoAsistio(_confirmadaId)).StatusCode);

        var segundo = await MarcarNoAsistio(_confirmadaId);

        Assert.Equal(HttpStatusCode.Conflict, segundo.StatusCode);
        Assert.Contains("La reserva ya fue marcada como no asistida.", await segundo.Content.ReadAsStringAsync());
        Assert.Single(_registro.NoAsistidas);
    }

    [Fact]
    public async Task Reserva_Inexistente_Responde_404()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));

        var respuesta = await MarcarNoAsistio(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Contains("No encontramos esa reserva.", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Check_In_Despues_De_Marcarla_Responde_409_Y_La_Reserva_Sigue_NoAsistio()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));
        Assert.Equal(HttpStatusCode.OK, (await MarcarNoAsistio(_confirmadaId)).StatusCode);

        var checkIn = await _cliente.PatchAsJsonAsync($"/api/reservas/{_confirmadaId}/check-in", new CheckInRequest(2));

        Assert.Equal(HttpStatusCode.Conflict, checkIn.StatusCode);
        Assert.Contains("La reserva fue marcada como no asistida. Si el cliente llegó, regístralo como visita sin reserva.",
            await checkIn.Content.ReadAsStringAsync());
        Assert.Equal(EstadoReserva.NoAsistio, (await LeerAsync(_confirmadaId)).Estado);
    }
}
