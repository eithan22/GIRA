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
using System.Text.Json;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Pruebas HTTP de PATCH /api/reservas/{id}/cancelar (GI-223, GI-238). El reloj del servicio marca
/// las 13:00 del 1 de diciembre de 2026 en Santo Domingo: el Almuerzo (12:00) ya empezó y la Cena
/// (18:00) todavía no.
/// </summary>
public class CancelarPorPersonalEndpointsTests : IAsyncLifetime
{
    private static readonly DateOnly Dia = new(2026, 12, 1);

    private readonly RegistroEventos _registro = new();
    private GiraApiFactoryConReloj _factory = null!;
    private HttpClient _cliente = null!;
    private Guid _cenaId;
    private Guid _cenaFranjaId;
    private Guid _almuerzoId;
    private Guid _enCursoId;

    public async Task InitializeAsync()
    {
        _factory = new GiraApiFactoryConReloj(
            new DateTimeOffset(2026, 12, 1, 17, 0, 0, TimeSpan.Zero),
            servicios =>
            {
                servicios.AddSingleton(_registro);
                // Primero el que anota, luego el que falla: ForeachAwaitPublisher llega a ambos.
                servicios.AddTransient<INotificationHandler<ReservaCanceladaEvent>, HandlerCancelacionRegistrador>();
                servicios.AddTransient<INotificationHandler<ReservaCanceladaEvent>, HandlerCancelacionQueFalla>();
            });
        _cliente = _factory.CreateClient();

        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        var zona = new Zona { Id = Guid.NewGuid(), Nombre = "Zona Cancelar" };
        var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 9501, Capacidad = 4, ZonaId = zona.Id };
        var almuerzo = new FranjaHoraria
        {
            Id = Guid.NewGuid(), Nombre = "Almuerzo Cancelar", HoraInicio = new TimeOnly(12, 0), HoraFin = new TimeOnly(15, 0)
        };
        var cena = new FranjaHoraria
        {
            Id = Guid.NewGuid(), Nombre = "Cena Cancelar", HoraInicio = new TimeOnly(18, 0), HoraFin = new TimeOnly(22, 30)
        };
        var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Cliente Cancelar", Telefono = "8095550606" };
        var enCena = NuevaReserva("60606060", cliente, mesa, cena);
        var enAlmuerzo = NuevaReserva("60606061", cliente, mesa, almuerzo);
        var enCurso = NuevaReserva("60606062", cliente, mesa, cena);
        enCurso.HacerCheckIn(2, DateTime.UtcNow);
        contexto.Zonas.Add(zona);
        contexto.Mesas.Add(mesa);
        contexto.FranjasHorarias.AddRange(almuerzo, cena);
        contexto.Clientes.Add(cliente);
        contexto.Reservas.AddRange(enCena, enAlmuerzo, enCurso);
        await contexto.SaveChangesAsync();

        _cenaId = enCena.Id;
        _cenaFranjaId = cena.Id;
        _almuerzoId = enAlmuerzo.Id;
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

    private Task<HttpResponseMessage> Cancelar(Guid id, string motivo) =>
        _cliente.PatchAsJsonAsync($"/api/reservas/{id}/cancelar", new CancelarPorPersonalRequest(motivo));

    private async Task<Reserva> LeerAsync(Guid id)
    {
        using var scope = _factory.Services.CreateScope();
        var contexto = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        return await contexto.Reservas.AsNoTracking().SingleAsync(r => r.Id == id);
    }

    [Fact]
    public async Task Sin_Token_Responde_401()
    {
        var respuesta = await Cancelar(_cenaId, "Motivo");

        Assert.Equal(HttpStatusCode.Unauthorized, respuesta.StatusCode);
    }

    [Fact]
    public async Task Rol_Sin_OperarReservas_Responde_403_Y_No_Cambia_La_Reserva()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Cocina));

        var respuesta = await Cancelar(_cenaId, "Motivo");

        Assert.Equal(HttpStatusCode.Forbidden, respuesta.StatusCode);
        Assert.Equal(EstadoReserva.Confirmada, (await LeerAsync(_cenaId)).Estado);
        Assert.Empty(_registro.Cancelaciones);
    }

    [Theory]
    [InlineData(Roles.Mesero)]
    [InlineData(Roles.Supervisor)]
    [InlineData(Roles.Gerente)]
    [InlineData(Roles.Administrador)]
    public async Task Con_Cada_Rol_Responde_200_Guarda_El_Motivo_Y_Registra_El_Usuario(string rol)
    {
        var usuarioId = Guid.NewGuid();
        _cliente.UsarToken(GenerarToken(usuarioId, rol));

        var respuesta = await Cancelar(_cenaId, "  Se enfermó el invitado principal  ");

        Assert.Equal(HttpStatusCode.OK, respuesta.StatusCode);
        var detalle = await respuesta.Content.ReadFromJsonAsync<ReservaDetalleDto>();
        Assert.Equal("Cancelada", detalle!.Estado);
        Assert.Equal("Se enfermó el invitado principal", detalle.MotivoCancelacion);
        var guardada = await LeerAsync(_cenaId);
        Assert.Equal(EstadoReserva.Cancelada, guardada.Estado);
        Assert.Equal(usuarioId, guardada.UpdatedBy);

        // Aunque HandlerCancelacionQueFalla lanza, responde 200 y queda guardada. El evento llega una
        // sola vez con fecha, franja y personas, y sin el motivo.
        var evento = Assert.Single(_registro.Cancelaciones);
        Assert.Equal(new ReservaCanceladaEvent(_cenaId, Dia, _cenaFranjaId, 2), evento);
        Assert.DoesNotContain("invitado", JsonSerializer.Serialize(evento));
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Motivo_Vacio_Responde_400_Con_El_Mensaje(string motivo)
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));

        var respuesta = await Cancelar(_cenaId, motivo);

        Assert.Equal(HttpStatusCode.BadRequest, respuesta.StatusCode);
        Assert.Contains("El motivo de la cancelación es obligatorio.", await respuesta.Content.ReadAsStringAsync());
        Assert.Equal(EstadoReserva.Confirmada, (await LeerAsync(_cenaId)).Estado);
    }

    [Fact]
    public async Task Reserva_Inexistente_Responde_404()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));

        var respuesta = await Cancelar(Guid.NewGuid(), "Motivo");

        Assert.Equal(HttpStatusCode.NotFound, respuesta.StatusCode);
        Assert.Contains("No encontramos esa reserva.", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Reserva_EnCurso_Responde_409_Con_El_Mensaje_Del_Estado()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));

        var respuesta = await Cancelar(_enCursoId, "Motivo");

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Contains("Esta reserva ya tiene el check-in registrado; no se puede cancelar.", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Franja_Que_Ya_Empezo_Responde_409()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));

        var respuesta = await Cancelar(_almuerzoId, "Motivo");

        Assert.Equal(HttpStatusCode.Conflict, respuesta.StatusCode);
        Assert.Contains("La franja de esta reserva ya empezó; no se puede cancelar.", await respuesta.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Un_Segundo_Cancelar_Responde_409_Y_Conserva_El_Primer_Motivo()
    {
        _cliente.UsarToken(GenerarToken(Guid.NewGuid(), Roles.Mesero));
        Assert.Equal(HttpStatusCode.OK, (await Cancelar(_cenaId, "Primer motivo")).StatusCode);

        var segundo = await Cancelar(_cenaId, "Otro motivo");

        Assert.Equal(HttpStatusCode.Conflict, segundo.StatusCode);
        Assert.Equal("Primer motivo", (await LeerAsync(_cenaId)).MotivoCancelacion);
        Assert.Single(_registro.Cancelaciones);
    }
}
