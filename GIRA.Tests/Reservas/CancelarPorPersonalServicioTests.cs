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
/// Pruebas de la cancelación por el personal (GI-223, GI-237): motivo obligatorio, límite en el
/// inicio de la franja (RN-RES-04), estados, mensajes y que la reserva nunca se elimina.
/// Repositorios reales sobre EF Core InMemory y reloj simulado (hora local = UTC-4).
/// </summary>
public class CancelarPorPersonalServicioTests
{
    // Almuerzo 12:00-15:00 y Cena 18:00-22:30; "hoy" = 1 de diciembre de 2026, mañana = 2 de diciembre.
    private static readonly DateOnly Hoy = new(2026, 12, 1);
    private static readonly DateOnly Manana = new(2026, 12, 2);

    private static DateTimeOffset Utc(DateOnly dia, int hora, int minuto) =>
        new(dia.ToDateTime(new TimeOnly(hora, minuto)).AddHours(4), TimeSpan.Zero);

    private sealed class Entorno : IDisposable
    {
        public DbContextOptions<GiraDbContext> Opciones { get; }
        public GiraDbContext Contexto { get; }
        public ReservaService Servicio { get; }
        public PublisherFalso Publisher { get; } = new();
        public Reserva Reserva { get; private set; } = null!;
        public FranjaHoraria Franja { get; private set; } = null!;
        public Cliente Cliente { get; private set; } = null!;

        public Entorno(
            DateTimeOffset ahoraUtc, bool conflictoAlGuardar = false, IPublisher? publisher = null,
            ILogger<PublicadorEventosSeguro>? logger = null)
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
            publisher ??= Publisher;

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

        public async Task SembrarAsync(
            DateOnly fecha, int horaInicio = 12, int horaFin = 15, EstadoReserva estado = EstadoReserva.Confirmada)
        {
            var zona = new Zona { Id = Guid.NewGuid(), Nombre = "Salón" };
            var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 2, Capacidad = 4, ZonaId = zona.Id };
            var franja = Franja = new FranjaHoraria
            {
                Id = Guid.NewGuid(), Nombre = "Franja", HoraInicio = new TimeOnly(horaInicio, 0), HoraFin = new TimeOnly(horaFin, 0)
            };
            Cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Cliente Ejemplo", Telefono = "8095550101" };
            Reserva = new Reserva
            {
                Id = Guid.NewGuid(), Codigo = "10000001", ClienteId = Cliente.Id, MesaId = mesa.Id,
                FranjaHorariaId = franja.Id, Fecha = fecha, Personas = 3, Origen = OrigenReserva.Telefono
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
                    Reserva.Cancelar("Antes");
                    break;
                case EstadoReserva.NoAsistio:
                    Reserva.MarcarNoAsistio();
                    break;
            }
            Contexto.Zonas.Add(zona);
            Contexto.Mesas.Add(mesa);
            Contexto.FranjasHorarias.Add(franja);
            Contexto.Clientes.Add(Cliente);
            Contexto.Reservas.Add(Reserva);
            await Contexto.SaveChangesAsync();
        }

        public Task<OperationResult<ReservaDetalleDto>> CancelarAsync(string motivo) =>
            Servicio.CancelarPorPersonalAsync(Reserva.Id, new CancelarPorPersonalRequest(motivo), default);

        public void Dispose() => Contexto.Dispose();
    }

    private static void EsFallo(OperationResult<ReservaDetalleDto> r, ErrorType tipo, string mensaje)
    {
        Assert.False(r.IsSuccess);
        Assert.Equal(tipo, r.Type);
        Assert.Equal(mensaje, r.Error);
    }

    // --- Caso feliz ---

    [Fact]
    public async Task Cancelar_Pasa_A_Cancelada_Guarda_El_Motivo_Recortado_Y_Devuelve_El_Detalle()
    {
        using var e = new Entorno(Utc(Hoy, 10, 0));
        await e.SembrarAsync(Hoy);

        var resultado = await e.CancelarAsync("  El cliente llamó para cancelar  ");

        Assert.True(resultado.IsSuccess);
        Assert.Equal("Cancelada", resultado.Value!.Estado);
        Assert.Equal("El cliente llamó para cancelar", resultado.Value.MotivoCancelacion);
        var guardada = await e.Contexto.Reservas.SingleAsync();
        Assert.Equal(EstadoReserva.Cancelada, guardada.Estado);
        Assert.Equal("El cliente llamó para cancelar", guardada.MotivoCancelacion);
    }

    [Fact]
    public async Task La_Reserva_Cancelada_No_Se_Elimina_Libera_La_Mesa_Y_El_Cliente_La_Sigue_Viendo_Cancelada()
    {
        using var e = new Entorno(Utc(Hoy, 10, 0));
        await e.SembrarAsync(Hoy);
        await e.CancelarAsync("Motivo interno");

        Assert.Equal(1, await e.Contexto.Reservas.IgnoreQueryFilters().CountAsync());
        Assert.False(await new ReservaRepository(e.Contexto).ExisteVigenteAsync(
            e.Reserva.MesaId, e.Reserva.Fecha, e.Reserva.FranjaHorariaId, default));
        var consulta = await e.Servicio.ConsultarPorCodigoAsync(
            new ConsultarReservaRequest(e.Reserva.Codigo, e.Cliente.Telefono), default);
        Assert.Equal("Cancelada", consulta.Value!.Estado);
    }

    // --- Límite: hasta el inicio de la franja ---

    [Fact]
    public async Task Un_Minuto_Antes_Del_Inicio_De_La_Franja_Se_Puede_Cancelar()
    {
        using var e = new Entorno(Utc(Hoy, 11, 59));
        await e.SembrarAsync(Hoy);

        Assert.True((await e.CancelarAsync("Motivo")).IsSuccess);
    }

    [Fact]
    public async Task Exactamente_En_El_Inicio_De_La_Franja_Es_Conflict()
    {
        using var e = new Entorno(Utc(Hoy, 12, 0));
        await e.SembrarAsync(Hoy);

        var resultado = await e.CancelarAsync("Motivo");

        EsFallo(resultado, ErrorType.Conflict, "La franja de esta reserva ya empezó; no se puede cancelar.");
        Assert.Equal(EstadoReserva.Confirmada, (await e.Contexto.Reservas.SingleAsync()).Estado);
    }

    [Fact]
    public async Task Una_Reserva_De_Hoy_Con_Franja_Futura_Se_Puede_Cancelar()
    {
        using var e = new Entorno(Utc(Hoy, 13, 0));
        await e.SembrarAsync(Hoy, horaInicio: 18, horaFin: 22); // Cena de hoy, todavía no empieza

        Assert.True((await e.CancelarAsync("Motivo")).IsSuccess);
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(12, 30)]
    [InlineData(23, 59)]
    public async Task Una_Reserva_De_Un_Dia_Futuro_Se_Cancela_A_Cualquier_Hora_Del_Dia_Anterior(int hora, int minuto)
    {
        using var e = new Entorno(Utc(Hoy, hora, minuto));
        await e.SembrarAsync(Manana);

        Assert.True((await e.CancelarAsync("Motivo")).IsSuccess);
    }

    // --- Motivo ---

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task Motivo_Vacio_O_Solo_Espacios_Es_Validation_Y_No_Cambia_La_Reserva(string motivo)
    {
        using var e = new Entorno(Utc(Hoy, 10, 0));
        await e.SembrarAsync(Hoy);

        var resultado = await e.CancelarAsync(motivo);

        EsFallo(resultado, ErrorType.Validation, "El motivo de la cancelación es obligatorio.");
        Assert.Equal(EstadoReserva.Confirmada, (await e.Contexto.Reservas.SingleAsync()).Estado);
    }

    [Fact]
    public async Task Motivo_De_500_Caracteres_Pasa_Y_De_501_Es_Validation()
    {
        using var e = new Entorno(Utc(Hoy, 10, 0));
        await e.SembrarAsync(Hoy);

        var largo = await e.CancelarAsync(new string('a', 501));
        var justo = await e.CancelarAsync($"  {new string('a', 500)}  "); // se recorta antes de contar

        EsFallo(largo, ErrorType.Validation, "El motivo no puede superar 500 caracteres.");
        Assert.True(justo.IsSuccess);
    }

    // --- Estados ---

    [Theory]
    [InlineData(EstadoReserva.EnCurso, "Esta reserva ya tiene el check-in registrado; no se puede cancelar.")]
    [InlineData(EstadoReserva.Cancelada, "La reserva ya fue cancelada.")]
    [InlineData(EstadoReserva.NoAsistio, "La reserva fue marcada como no asistida; no se puede cancelar.")]
    [InlineData(EstadoReserva.Completada, "La reserva ya fue completada; no se puede cancelar.")]
    public async Task Cancelar_Fuera_De_Estado_Confirmada_Es_Conflict_Con_El_Mensaje_Del_Estado(
        EstadoReserva estado, string mensaje)
    {
        using var e = new Entorno(Utc(Hoy, 10, 0));
        await e.SembrarAsync(Hoy, estado: estado);

        var resultado = await e.CancelarAsync("Motivo");

        EsFallo(resultado, ErrorType.Conflict, mensaje);
    }

    [Fact]
    public async Task Un_Segundo_Cancelar_Es_Conflict_Y_No_Cambia_El_Motivo()
    {
        using var e = new Entorno(Utc(Hoy, 10, 0));
        await e.SembrarAsync(Hoy);
        Assert.True((await e.CancelarAsync("Primer motivo")).IsSuccess);

        var segundo = await e.CancelarAsync("Otro motivo");

        EsFallo(segundo, ErrorType.Conflict, "La reserva ya fue cancelada.");
        Assert.Equal("Primer motivo", (await e.Contexto.Reservas.SingleAsync()).MotivoCancelacion);
    }

    // --- Evento (SRS 3.4: ReservaId, Fecha, FranjaHorariaId, Personas; sin motivo) ---

    [Fact]
    public async Task Cancelar_Publica_El_Evento_Una_Vez_Con_Fecha_Franja_Y_Personas_Y_Sin_El_Motivo()
    {
        using var e = new Entorno(Utc(Hoy, 10, 0));
        await e.SembrarAsync(Hoy);

        await e.CancelarAsync("Motivo con datos personales: llamó Juan Pérez");

        var evento = Assert.IsType<ReservaCanceladaEvent>(Assert.Single(e.Publisher.Publicados));
        Assert.Equal(new ReservaCanceladaEvent(e.Reserva.Id, Hoy, e.Franja.Id, 3), evento);
        Assert.DoesNotContain("Juan", evento.ToString());
        Assert.DoesNotContain("Motivo", evento.ToString());
        Assert.Equal(
            ["ReservaId", "Fecha", "FranjaHorariaId", "Personas"],
            typeof(ReservaCanceladaEvent).GetProperties().Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task Cancelar_Publica_El_Evento_Despues_De_Guardar()
    {
        EstadoReserva? estadoAlPublicar = null;
        Entorno? entorno = null;
        var publisher = new PublisherQueVerifica(async () =>
        {
            // Un contexto nuevo sobre la misma base: solo ve lo que ya está guardado.
            using var otro = new GiraDbContext(entorno!.Opciones);
            estadoAlPublicar = (await otro.Reservas.SingleAsync()).Estado;
        });
        using var e = new Entorno(Utc(Hoy, 10, 0), publisher: publisher);
        entorno = e;
        await e.SembrarAsync(Hoy);

        await e.CancelarAsync("Motivo");

        Assert.Equal(EstadoReserva.Cancelada, estadoAlPublicar);
        Assert.Single(publisher.Publicados);
    }

    [Fact]
    public async Task Un_Segundo_Cancelar_No_Vuelve_A_Publicar()
    {
        using var e = new Entorno(Utc(Hoy, 10, 0));
        await e.SembrarAsync(Hoy);
        await e.CancelarAsync("Primer motivo");

        await e.CancelarAsync("Otro motivo");

        Assert.Single(e.Publisher.Publicados);
    }

    [Fact]
    public async Task Si_La_Cancelacion_Falla_No_Se_Publica_Ningun_Evento()
    {
        using var e = new Entorno(Utc(Hoy, 12, 0)); // la franja ya empezó
        await e.SembrarAsync(Hoy);

        await e.CancelarAsync("Motivo");
        await e.CancelarAsync("");

        Assert.Empty(e.Publisher.Publicados);
    }

    [Fact]
    public async Task Un_Handler_Que_Falla_No_Provoca_Error_Ni_Revierte_Y_No_Registra_El_Motivo_En_El_Log()
    {
        var publisher = new PublisherQueFalla();
        var logger = new LoggerCapturador<PublicadorEventosSeguro>();
        using var e = new Entorno(Utc(Hoy, 10, 0), publisher: publisher, logger: logger);
        await e.SembrarAsync(Hoy);

        var resultado = await e.CancelarAsync("Motivo con datos personales: llamó Juan Pérez");

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, publisher.Intentos);
        Assert.Equal(EstadoReserva.Cancelada, (await e.Contexto.Reservas.SingleAsync()).Estado);
        var entrada = Assert.Single(logger.Entradas);
        Assert.Equal(LogLevel.Error, entrada.Nivel);
        Assert.Contains("ReservaCanceladaEvent", entrada.Mensaje);
        Assert.Contains($"ReservaId={e.Reserva.Id}", entrada.Mensaje);
        Assert.DoesNotContain("Juan", entrada.Mensaje);
    }

    // --- Otros errores ---

    [Fact]
    public async Task Cancelar_Una_Reserva_Inexistente_Es_NotFound()
    {
        using var e = new Entorno(Utc(Hoy, 10, 0));

        var resultado = await e.Servicio.CancelarPorPersonalAsync(Guid.NewGuid(), new CancelarPorPersonalRequest("Motivo"), default);

        EsFallo(resultado, ErrorType.NotFound, "No encontramos esa reserva.");
    }

    [Fact]
    public async Task Cancelar_Con_Conflicto_De_Concurrencia_Es_Conflict_Con_Mensaje_Propio()
    {
        using var e = new Entorno(Utc(Hoy, 10, 0), conflictoAlGuardar: true);
        await e.SembrarAsync(Hoy);

        var resultado = await e.CancelarAsync("Motivo");

        EsFallo(resultado, ErrorType.Conflict, "La reserva cambió mientras se cancelaba; vuelve a consultarla.");
    }
}
