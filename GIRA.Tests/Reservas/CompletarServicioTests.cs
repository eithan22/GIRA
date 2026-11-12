using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Events.Reservas;
using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Application.Services.Reservas;
using GIRA.Application.Validations.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Repositories.Reservas;
using GIRA.Infrastructure.Services.Reservas;
using GIRA.Tests.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Pruebas de completar la visita (GI-166, GI-202): transición EnCurso a Completada, evento
/// <see cref="VisitaCompletadaEvent"/> una sola vez y después de guardar, mensajes por estado y
/// handler que falla sin revertir. Repositorios reales sobre EF Core InMemory.
/// </summary>
public class CompletarServicioTests
{
    private static readonly DateOnly Dia = new(2026, 12, 1);

    private sealed class Entorno : IDisposable
    {
        public DbContextOptions<GiraDbContext> Opciones { get; }
        public GiraDbContext Contexto { get; }
        public ReservaService Servicio { get; }
        public Reserva Reserva { get; private set; } = null!;
        public FranjaHoraria Franja { get; private set; } = null!;

        public Entorno(IPublisher publisher, ILogger<PublicadorEventosSeguro>? logger = null, bool conflictoAlGuardar = false)
        {
            Opciones = new DbContextOptionsBuilder<GiraDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            Contexto = new GiraDbContext(Opciones);

            var reloj = new TimeProviderFalso(new DateTimeOffset(2026, 12, 1, 16, 0, 0, TimeSpan.Zero));
            var parametros = new ParametrosReservaFalso();
            IReservaRepository reservas = new ReservaRepository(Contexto);
            if (conflictoAlGuardar)
            {
                reservas = new ReservaRepositoryConConflicto(reservas);
            }

            Servicio = new ReservaService(
                new MesaRepository(Contexto),
                new FranjaHorariaRepository(Contexto),
                reservas,
                new ClienteRepository(Contexto),
                new MesaGestionRepository(Contexto),
                parametros,
                new CrearReservaRequestValidator(parametros, reloj),
                new ListarReservasRequestValidator(),
                new CheckInRequestValidator(),
                new CancelarPorPersonalRequestValidator(),
                new ModificarReservaRequestValidator(),
                new GeneradorCodigoReserva(),
                publisher,
                new PublicadorEventosSeguro(publisher, logger ?? NullLogger<PublicadorEventosSeguro>.Instance),
                reloj);
        }

        public async Task SembrarAsync(EstadoReserva estado = EstadoReserva.EnCurso)
        {
            var zona = new Zona { Id = Guid.NewGuid(), Nombre = "Salón" };
            var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 2, Capacidad = 4, ZonaId = zona.Id };
            Franja = new FranjaHoraria
            {
                Id = Guid.NewGuid(), Nombre = "Almuerzo", HoraInicio = new TimeOnly(12, 0), HoraFin = new TimeOnly(15, 0)
            };
            var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Cliente Ejemplo", Telefono = "8095550101" };
            // Reservada para 2 personas; en el check-in llegaron 3 (para distinguir Personas de PersonasReales).
            Reserva = new Reserva
            {
                Id = Guid.NewGuid(), Codigo = "10000001", ClienteId = cliente.Id, MesaId = mesa.Id,
                FranjaHorariaId = Franja.Id, Fecha = Dia, Personas = 2, Origen = OrigenReserva.Telefono
            };
            switch (estado)
            {
                case EstadoReserva.EnCurso:
                    Reserva.HacerCheckIn(3, DateTime.UtcNow);
                    break;
                case EstadoReserva.Completada:
                    Reserva.HacerCheckIn(3, DateTime.UtcNow);
                    Reserva.Completar();
                    break;
                case EstadoReserva.Cancelada:
                    Reserva.Cancelar("Prueba");
                    break;
                case EstadoReserva.NoAsistio:
                    Reserva.MarcarNoAsistio();
                    break;
            }
            Contexto.Zonas.Add(zona);
            Contexto.Mesas.Add(mesa);
            Contexto.FranjasHorarias.Add(Franja);
            Contexto.Clientes.Add(cliente);
            Contexto.Reservas.Add(Reserva);
            await Contexto.SaveChangesAsync();
        }

        public Task<OperationResult<ReservaDetalleDto>> CompletarAsync() => Servicio.CompletarAsync(Reserva.Id, default);

        public void Dispose() => Contexto.Dispose();
    }

    private static void EsFallo(OperationResult<ReservaDetalleDto> r, ErrorType tipo, string mensaje)
    {
        Assert.False(r.IsSuccess);
        Assert.Equal(tipo, r.Type);
        Assert.Equal(mensaje, r.Error);
    }

    [Fact]
    public async Task Completar_Pasa_A_Completada_Y_Publica_El_Evento_Una_Vez_Con_Las_Personas_Reales()
    {
        var publisher = new PublisherFalso();
        using var e = new Entorno(publisher);
        await e.SembrarAsync();

        var resultado = await e.CompletarAsync();

        Assert.True(resultado.IsSuccess);
        Assert.Equal("Completada", resultado.Value!.Estado);
        Assert.Equal(EstadoReserva.Completada, (await e.Contexto.Reservas.SingleAsync()).Estado);
        var evento = Assert.IsType<VisitaCompletadaEvent>(Assert.Single(publisher.Publicados));
        // PersonasReales de la reserva (3), no las reservadas (2); con reserva.
        Assert.Equal(new VisitaCompletadaEvent(Dia, e.Franja.Id, 3, ConReserva: true), evento);
    }

    [Fact]
    public async Task Completar_Publica_El_Evento_Despues_De_Guardar()
    {
        EstadoReserva? estadoAlPublicar = null;
        Entorno? entorno = null;
        var publisher = new PublisherQueVerifica(async () =>
        {
            // Un contexto nuevo sobre la misma base: solo ve lo que ya está guardado.
            using var otro = new GiraDbContext(entorno!.Opciones);
            estadoAlPublicar = (await otro.Reservas.SingleAsync()).Estado;
        });
        using var e = new Entorno(publisher);
        entorno = e;
        await e.SembrarAsync();

        await e.CompletarAsync();

        Assert.Equal(EstadoReserva.Completada, estadoAlPublicar);
        Assert.Single(publisher.Publicados);
    }

    [Fact]
    public async Task Un_Segundo_Completar_Es_Conflict_Y_El_Evento_Sigue_Siendo_Uno()
    {
        var publisher = new PublisherFalso();
        using var e = new Entorno(publisher);
        await e.SembrarAsync();
        Assert.True((await e.CompletarAsync()).IsSuccess);

        var segundo = await e.CompletarAsync();

        EsFallo(segundo, ErrorType.Conflict, "La reserva ya fue completada.");
        Assert.Single(publisher.Publicados);
    }

    [Theory]
    [InlineData(EstadoReserva.Confirmada, "Aún no se hizo el check-in; no se puede completar.")]
    [InlineData(EstadoReserva.Completada, "La reserva ya fue completada.")]
    [InlineData(EstadoReserva.Cancelada, "La reserva fue cancelada; no se puede completar.")]
    [InlineData(EstadoReserva.NoAsistio, "La reserva fue marcada como no asistida; no se puede completar.")]
    public async Task Completar_Fuera_De_EnCurso_Es_Conflict_Con_El_Mensaje_Del_Estado_Y_No_Publica(
        EstadoReserva estado, string mensaje)
    {
        var publisher = new PublisherFalso();
        using var e = new Entorno(publisher);
        await e.SembrarAsync(estado);

        var resultado = await e.CompletarAsync();

        EsFallo(resultado, ErrorType.Conflict, mensaje);
        Assert.Empty(publisher.Publicados);
    }

    [Fact]
    public async Task Completar_Una_Reserva_Inexistente_Es_NotFound()
    {
        using var e = new Entorno(new PublisherFalso());

        var resultado = await e.Servicio.CompletarAsync(Guid.NewGuid(), default);

        EsFallo(resultado, ErrorType.NotFound, "No encontramos esa reserva.");
    }

    [Fact]
    public async Task Completar_Con_Conflicto_De_Concurrencia_Es_Conflict_Con_Mensaje_Propio_Y_No_Publica()
    {
        var publisher = new PublisherFalso();
        using var e = new Entorno(publisher, conflictoAlGuardar: true);
        await e.SembrarAsync();

        var resultado = await e.CompletarAsync();

        EsFallo(resultado, ErrorType.Conflict, "La reserva cambió mientras se completaba la visita; vuelve a consultarla.");
        Assert.Empty(publisher.Publicados);
    }

    [Fact]
    public async Task Un_Handler_Que_Falla_No_Provoca_Error_Ni_Revierte_Y_Se_Registra_Sin_Datos_Personales()
    {
        var publisher = new PublisherQueFalla();
        var logger = new LoggerCapturador<PublicadorEventosSeguro>();
        using var e = new Entorno(publisher, logger);
        await e.SembrarAsync();

        var resultado = await e.CompletarAsync();

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, publisher.Intentos);
        Assert.Equal(EstadoReserva.Completada, (await e.Contexto.Reservas.SingleAsync()).Estado);
        var entrada = Assert.Single(logger.Entradas);
        Assert.Equal(LogLevel.Error, entrada.Nivel);
        Assert.NotNull(entrada.Excepcion);
        Assert.Contains("VisitaCompletadaEvent", entrada.Mensaje);
        Assert.Contains($"ReservaId={e.Reserva.Id}", entrada.Mensaje);
        Assert.DoesNotContain("Cliente Ejemplo", entrada.Mensaje);
        Assert.DoesNotContain("8095550101", entrada.Mensaje);
    }
}
