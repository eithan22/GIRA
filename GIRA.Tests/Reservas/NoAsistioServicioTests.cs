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
using Microsoft.Extensions.Logging.Abstractions;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Pruebas de marcar una reserva como no asistida por el personal (GI-404): solo desde Confirmada y con la
/// franja ya empezada, evento una sola vez y después de guardar, mesa liberada, conflicto de concurrencia y
/// handler que falla sin revertir. Repositorios reales sobre EF Core InMemory. La franja de las pruebas va de
/// 12:00 a 15:00 (hora local = UTC-4).
/// </summary>
public class NoAsistioServicioTests
{
    private static readonly DateOnly Dia = new(2026, 12, 1);
    private static readonly DateTimeOffset UnMinutoAntesDelInicio = new(2026, 12, 1, 15, 59, 0, TimeSpan.Zero); // 11:59 local
    private static readonly DateTimeOffset EnElInicio = new(2026, 12, 1, 16, 0, 0, TimeSpan.Zero);             // 12:00 local

    private sealed class Entorno : IDisposable
    {
        public DbContextOptions<GiraDbContext> Opciones { get; }
        public GiraDbContext Contexto { get; }
        public ReservaService Servicio { get; }
        public DisponibilidadService Disponibilidad { get; }
        public Reserva Reserva { get; private set; } = null!;
        public FranjaHoraria Franja { get; private set; } = null!;

        public Entorno(
            IPublisher publisher, DateTimeOffset ahoraUtc, bool conflictoAlGuardar = false)
        {
            Opciones = new DbContextOptionsBuilder<GiraDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            Contexto = new GiraDbContext(Opciones);

            var reloj = new TimeProviderFalso(ahoraUtc);
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
                new PublicadorEventosSeguro(publisher, NullLogger<PublicadorEventosSeguro>.Instance),
                reloj);

            Disponibilidad = new DisponibilidadService(
                new MesaRepository(Contexto),
                new FranjaHorariaRepository(Contexto),
                new ReservaRepository(Contexto),
                parametros,
                new ConsultarDisponibilidadRequestValidator(parametros, reloj),
                reloj);
        }

        public async Task SembrarAsync(EstadoReserva estado = EstadoReserva.Confirmada)
        {
            var zona = new Zona { Id = Guid.NewGuid(), Nombre = "Salón" };
            var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 2, Capacidad = 4, ZonaId = zona.Id };
            Franja = new FranjaHoraria
            {
                Id = Guid.NewGuid(), Nombre = "Almuerzo", HoraInicio = new TimeOnly(12, 0), HoraFin = new TimeOnly(15, 0)
            };
            var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Cliente Ejemplo", Telefono = "8095550101" };
            Reserva = new Reserva
            {
                Id = Guid.NewGuid(), Codigo = "10000001", ClienteId = cliente.Id, MesaId = mesa.Id,
                FranjaHorariaId = Franja.Id, Fecha = Dia, Personas = 3, Origen = OrigenReserva.Telefono
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

        public Task<OperationResult<ReservaDetalleDto>> MarcarAsync() => Servicio.MarcarNoAsistioAsync(Reserva.Id, default);

        public void Dispose() => Contexto.Dispose();
    }

    private static void EsFallo(OperationResult<ReservaDetalleDto> r, ErrorType tipo, string mensaje)
    {
        Assert.False(r.IsSuccess);
        Assert.Equal(tipo, r.Type);
        Assert.Equal(mensaje, r.Error);
    }

    [Fact]
    public async Task Con_La_Franja_Empezada_Pasa_A_NoAsistio_Y_Publica_El_Evento_Una_Vez()
    {
        var publisher = new PublisherFalso();
        using var e = new Entorno(publisher, EnElInicio); // el instante exacto del inicio ya cuenta
        await e.SembrarAsync();

        var resultado = await e.MarcarAsync();

        Assert.True(resultado.IsSuccess);
        Assert.Equal("NoAsistio", resultado.Value!.Estado);
        Assert.Equal(EstadoReserva.NoAsistio, (await e.Contexto.Reservas.SingleAsync()).Estado);
        // Lleva las personas reservadas (3): no hubo check-in.
        var evento = Assert.IsType<ReservaNoAsistidaEvent>(Assert.Single(publisher.Publicados));
        Assert.Equal(new ReservaNoAsistidaEvent(e.Reserva.Id, Dia, e.Franja.Id, 3), evento);
    }

    [Fact]
    public async Task Antes_Del_Inicio_De_La_Franja_Es_Conflict_Que_Remite_A_Cancelar_Y_No_Publica()
    {
        var publisher = new PublisherFalso();
        using var e = new Entorno(publisher, UnMinutoAntesDelInicio);
        await e.SembrarAsync();

        var resultado = await e.MarcarAsync();

        EsFallo(resultado, ErrorType.Conflict, "La franja aún no empieza; si el cliente no vendrá, cancela la reserva.");
        Assert.Equal(EstadoReserva.Confirmada, (await e.Contexto.Reservas.SingleAsync()).Estado);
        Assert.Empty(publisher.Publicados);
    }

    [Fact]
    public async Task Publica_El_Evento_Despues_De_Guardar()
    {
        EstadoReserva? estadoAlPublicar = null;
        Entorno? entorno = null;
        var publisher = new PublisherQueVerifica(async () =>
        {
            // Un contexto nuevo sobre la misma base: solo ve lo que ya está guardado.
            using var otro = new GiraDbContext(entorno!.Opciones);
            estadoAlPublicar = (await otro.Reservas.SingleAsync()).Estado;
        });
        using var e = new Entorno(publisher, EnElInicio);
        entorno = e;
        await e.SembrarAsync();

        await e.MarcarAsync();

        Assert.Equal(EstadoReserva.NoAsistio, estadoAlPublicar);
        Assert.Single(publisher.Publicados);
    }

    [Theory]
    [InlineData(EstadoReserva.EnCurso, "Esta reserva ya tiene el check-in registrado; no se puede marcar como no asistida.")]
    [InlineData(EstadoReserva.Completada, "La reserva ya fue completada; no se puede marcar como no asistida.")]
    [InlineData(EstadoReserva.Cancelada, "La reserva fue cancelada; no se puede marcar como no asistida.")]
    [InlineData(EstadoReserva.NoAsistio, "La reserva ya fue marcada como no asistida.")]
    public async Task Fuera_De_Confirmada_Es_Conflict_Con_El_Mensaje_Del_Estado_Y_No_Publica(
        EstadoReserva estado, string mensaje)
    {
        var publisher = new PublisherFalso();
        using var e = new Entorno(publisher, EnElInicio);
        await e.SembrarAsync(estado);

        var resultado = await e.MarcarAsync();

        EsFallo(resultado, ErrorType.Conflict, mensaje);
        Assert.Equal(estado, (await e.Contexto.Reservas.SingleAsync()).Estado);
        Assert.Empty(publisher.Publicados);
    }

    [Fact]
    public async Task Un_Segundo_Intento_Es_Conflict_Y_El_Evento_Sigue_Siendo_Uno()
    {
        var publisher = new PublisherFalso();
        using var e = new Entorno(publisher, EnElInicio);
        await e.SembrarAsync();
        Assert.True((await e.MarcarAsync()).IsSuccess);

        var segundo = await e.MarcarAsync();

        EsFallo(segundo, ErrorType.Conflict, "La reserva ya fue marcada como no asistida.");
        Assert.Single(publisher.Publicados);
    }

    [Fact]
    public async Task Una_Reserva_Inexistente_Es_NotFound()
    {
        using var e = new Entorno(new PublisherFalso(), EnElInicio);

        var resultado = await e.Servicio.MarcarNoAsistioAsync(Guid.NewGuid(), default);

        EsFallo(resultado, ErrorType.NotFound, "No encontramos esa reserva.");
    }

    [Fact]
    public async Task Un_Conflicto_De_Concurrencia_Es_Conflict_Sin_Evento()
    {
        var publisher = new PublisherFalso();
        using var e = new Entorno(publisher, EnElInicio, conflictoAlGuardar: true);
        await e.SembrarAsync();

        var resultado = await e.MarcarAsync();

        EsFallo(resultado, ErrorType.Conflict, "La reserva cambió mientras se marcaba como no asistida; vuelve a consultarla.");
        Assert.Empty(publisher.Publicados);
    }

    [Fact]
    public async Task Un_Handler_Que_Falla_No_Revierte_La_Operacion()
    {
        using var e = new Entorno(new PublisherQueFalla(), EnElInicio);
        await e.SembrarAsync();

        var resultado = await e.MarcarAsync();

        Assert.True(resultado.IsSuccess);
        Assert.Equal(EstadoReserva.NoAsistio, (await e.Contexto.Reservas.SingleAsync()).Estado);
    }

    [Fact]
    public async Task La_Mesa_Queda_Libre_En_La_Disponibilidad_Y_Para_Una_Reserva_Nueva()
    {
        using var e = new Entorno(new PublisherFalso(), EnElInicio);
        await e.SembrarAsync();
        var antes = await e.Disponibilidad.ConsultarAsync(
            new ConsultarDisponibilidadRequest(Dia, 2), default, AudienciaReserva.Personal);
        var vigenteAntes = await new ReservaRepository(e.Contexto)
            .ExisteVigenteAsync(e.Reserva.MesaId, Dia, e.Franja.Id, default);

        await e.MarcarAsync();

        var despues = await e.Disponibilidad.ConsultarAsync(
            new ConsultarDisponibilidadRequest(Dia, 2), default, AudienciaReserva.Personal);
        var vigenteDespues = await new ReservaRepository(e.Contexto)
            .ExisteVigenteAsync(e.Reserva.MesaId, Dia, e.Franja.Id, default);
        Assert.Empty(antes.Value!);          // la única mesa estaba ocupada
        Assert.True(vigenteAntes);
        Assert.Single(despues.Value!);       // la franja vuelve a ofrecerse
        Assert.False(vigenteDespues);        // y el índice de doble reserva ya no la bloquea
    }

    [Fact]
    public async Task El_Check_In_De_Una_Reserva_NoAsistio_Es_Conflict()
    {
        using var e = new Entorno(new PublisherFalso(), EnElInicio);
        await e.SembrarAsync();
        await e.MarcarAsync();

        var checkIn = await e.Servicio.CheckInAsync(e.Reserva.Id, new CheckInRequest(2), default);

        Assert.False(checkIn.IsSuccess);
        Assert.Equal(ErrorType.Conflict, checkIn.Type);
        Assert.Equal(
            "La reserva fue marcada como no asistida. Si el cliente llegó, regístralo como visita sin reserva.",
            checkIn.Error);
        Assert.Equal(EstadoReserva.NoAsistio, (await e.Contexto.Reservas.SingleAsync()).Estado);
    }
}
