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
/// Pruebas HTTP de PATCH /api/reservas/{id}/completar (GI-166, GI-204). Recorren el pipeline real
/// (JWT, política OperarReservas, MediatR) con dos handlers de prueba registrados: uno que anota el
/// evento y otro que siempre falla, para comprobar que un handler que falla no cambia la respuesta
/// ni revierte la reserva.
/// </summary>
public class CompletarEndpointsTests : IAsyncLifetime
{
    private static readonly DateOnly Dia = new(2026, 12, 1);

    private readonly RegistroEventos _registro = new();
    private GiraApiFactoryConReloj _factory = null!;
    private HttpClient _cliente = null!;
    private Guid _franjaId;
    private Guid _enCursoId;
    private Guid _confirmadaId;

    public async Task InitializeAsync()
    {
        _factory = new GiraApiFactoryConReloj(
            new DateTimeOffset(2026, 12, 1, 16, 0, 0, TimeSpan.Zero),
            servicios =>
            {
                servicios.AddSingleton(_registro);
                // Primero el que anota, luego el que falla: ForeachAwaitPublisher llega a ambos.
                servicios.AddTransient<INotificationHandler<VisitaCompletadaEvent>, HandlerRegistrador>();
                servicios.AddTransient<INotificationHandler<VisitaCompletadaEvent>, HandlerQueFalla>();
            });
        _cliente = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        var zona = new Zona { Id = Guid.NewGuid(), Nombre = "Zona Completar" };
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 9301, Capacidad = 4, ZonaId = zona.Id };
        var franja = new FranjaHoraria
        {
            Id = Guid.NewGuid(), Nombre = "Franja Completar", HoraInicio = new TimeOnly(12, 0), HoraFin = new TimeOnly(15, 0)
        };
        var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Cliente Completar", Telefono = "8095550404" };
        var enCurso = NuevaReserva("40404040", cliente, mesa, franja);
        enCurso.HacerCheckIn(3, DateTime.UtcNow); // reservada para 2, llegaron 3
        var confirmada = NuevaReserva("40404041", cliente, mesa, franja);
        contexto.Zonas.Add(zona);
        contexto.Mesas.Add(mesa);
        contexto.FranjasHorarias.Add(franja);
        contexto.Clientes.Add(cliente);
        contexto.Reservas.AddRange(enCurso, confirmada);
        await contexto.SaveChangesAsync();

        _franjaId = franja.Id;
        _enCursoId = enCurso.Id;
        _confirmadaId = confirmada.Id;
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

    private Task<HttpResponseMessage> Completar(Guid id) => _cliente.PatchAsync($"/api/reservas/{id}/completar", null);

    private async Task<Reserva> LeerAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        return await contexto.Reservas.AsNoTracking().SingleAsync(r => r.Id == id);
    }

    [Fact]
    public async Task Sin_Token_Responde_401()
    {
        var respuesta = await Completar(_enCursoId);

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Rol_Sin_OperarReservas_Responde_403_Sin_Cambiar_La_Reserva_Ni_Publicar()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Cocina));

        var respuesta = await Completar(_enCursoId);

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal(EstadoReserva.EnCurso, (await LeerAsync(_enCursoId)).Estado);
        Assert.Empty(_registro.Visitas);
    }

    [Theory]
    [InlineData(Roles.Mesero)]
    [InlineData(Roles.Supervisor)]
    [InlineData(Roles.Gerente)]
    [InlineData(Roles.Administrador)]
    public async Task Con_Cada_Rol_Responde_200_Completa_Y_Publica_El_Evento_Una_Vez(string rol)
    {
        var usuarioId = Guid.NewGuid();
        _cliente.UsarToken(GenerarToken(usuarioId, rol));

        var respuesta = await Completar(_enCursoId);

        // Aunque HandlerQueFalla lanza, la operación responde 200 y queda guardada.
        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var detalle = await respuesta.Content.ReadFromJsonAsync<ReservaDetalleDto>();
        Assert.Equal("Completada", detalle!.Estado);
        var guardada = await LeerAsync(_enCursoId);
        Assert.Equal(EstadoReserva.Completada, guardada.Estado);
        Assert.Equal(usuarioId, guardada.UpdatedBy);
        var evento = Assert.Single(_registro.Visitas);
        Assert.Equal(new VisitaCompletadaEvent(Dia, _franjaId, 3, ConReserva: true), evento);
    }

    [Fact]
    public async Task Un_Segundo_Completar_Responde_409_Y_No_Vuelve_A_Publicar()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));
        Assert.Equal(HttpStatusCode.OK, (await Completar(_enCursoId)).StatusCode);

        var segundo = await Completar(_enCursoId);

        Assert.Equal(HttpStatusCode.Conflict, segundo.StatusCode);
        Assert.Contains("La reserva ya fue completada.", await segundo.Content.ReadAsStringAsync());
        Assert.Single(_registro.Visitas);
    }

    [Fact]
    public async Task Reserva_Confirmada_Responde_409_Con_El_Mensaje_Del_Estado()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));

        var respuesta = await Completar(_confirmadaId);

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Contains("Aún no se hizo el check-in; no se puede completar.", await respuesta.Content.ReadAsStringAsync());
        Assert.Empty(_registro.Visitas);
    }

    [Fact]
    public async Task Reserva_Inexistente_Responde_404()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));

        var respuesta = await Completar(Guid.NewGuid());

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Contains("No encontramos esa reserva.", await respuesta.Content.ReadAsStringAsync());
    }
}
