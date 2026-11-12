using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Events.Reservas;
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
/// Pruebas del registro de visitas sin reserva (GI-166, GI-203): solo hoy, franja activa y ya
/// iniciada, mesa libre, tope de personas y evento con ConReserva = false. Repositorios reales
/// sobre EF Core InMemory y reloj simulado (hora local de Santo Domingo = UTC-4).
/// </summary>
public class VisitaServicioTests
{
    // Almuerzo 12:00-15:00 y Cena 18:00-22:30 del 1 de diciembre de 2026 (hora local).
    private static readonly DateOnly Dia = new(2026, 12, 1);

    private static DateTimeOffset Utc(int hora, int minuto) =>
        new(Dia.ToDateTime(new TimeOnly(hora, minuto)).AddHours(4), TimeSpan.Zero);

    private sealed class Entorno : IDisposable
    {
        public DbContextOptions<GiraDbContext> Opciones { get; }
        public GiraDbContext Contexto { get; }
        public VisitaService Servicio { get; }
        public Zona Zona { get; private set; } = null!;
        public Mesa Mesa { get; private set; } = null!;
        public FranjaHoraria Almuerzo { get; private set; } = null!;
        public FranjaHoraria Cena { get; private set; } = null!;

        public Entorno(DateTimeOffset ahoraUtc, IPublisher publisher, NullLogger<PublicadorEventosSeguro>? logger = null)
        {
            Opciones = new DbContextOptionsBuilder<GiraDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            Contexto = new GiraDbContext(Opciones);

            Servicio = new VisitaService(
                new VisitaRepository(Contexto),
                new FranjaGestionRepository(Contexto),
                new MesaGestionRepository(Contexto),
                new ZonaRepository(Contexto),
                new ReservaRepository(Contexto),
                new RegistrarVisitaRequestValidator(),
                new PublicadorEventosSeguro(publisher, logger ?? NullLogger<PublicadorEventosSeguro>.Instance),
                new TimeProviderFalso(ahoraUtc));
        }

        public async Task SembrarAsync(bool zonaActiva = true, bool mesaActiva = true, bool almuerzoActivo = true)
        {
            Zona = new Zona { Id = Guid.NewGuid(), Nombre = "Salón", Activa = zonaActiva };
            Mesa = new Mesa { Id = Guid.NewGuid(), Numero = 5, Capacidad = 4, ZonaId = Zona.Id, Activa = mesaActiva };
            Almuerzo = new FranjaHoraria
            {
                Id = Guid.NewGuid(), Nombre = "Almuerzo", HoraInicio = new TimeOnly(12, 0), HoraFin = new TimeOnly(15, 0),
                Activa = almuerzoActivo
            };
            Cena = new FranjaHoraria
            {
                Id = Guid.NewGuid(), Nombre = "Cena", HoraInicio = new TimeOnly(18, 0), HoraFin = new TimeOnly(22, 30)
            };
            Contexto.Zonas.Add(Zona);
            Contexto.Mesas.Add(Mesa);
            Contexto.FranjasHorarias.AddRange(Almuerzo, Cena);
            await Contexto.SaveChangesAsync();
        }

        public async Task SembrarReservaAsync(FranjaHoraria franja, EstadoReserva estado)
        {
            var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Cliente Ejemplo", Telefono = "8095550101" };
            var reserva = new Reserva
            {
                Id = Guid.NewGuid(), Codigo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), ClienteId = cliente.Id,
                MesaId = Mesa.Id, FranjaHorariaId = franja.Id, Fecha = Dia, Personas = 2, Origen = OrigenReserva.Telefono
            };
            switch (estado)
            {
                case EstadoReserva.EnCurso:
                    reserva.HacerCheckIn(2, DateTime.UtcNow);
                    break;
                case EstadoReserva.Cancelada:
                    reserva.Cancelar("Prueba");
                    break;
                case EstadoReserva.Completada:
                    reserva.HacerCheckIn(2, DateTime.UtcNow);
                    reserva.Completar();
                    break;
            }
            Contexto.Clientes.Add(cliente);
            Contexto.Reservas.Add(reserva);
            await Contexto.SaveChangesAsync();
        }

        public Task<OperationResult<VisitaDto>> RegistrarAsync(Guid franjaId, Guid? mesaId, int personas) =>
            Servicio.RegistrarAsync(new RegistrarVisitaRequest(franjaId, mesaId, personas), default);

        public void Dispose() => Contexto.Dispose();
    }

    private static void EsFallo(OperationResult<VisitaDto> r, ErrorType tipo, string mensaje)
    {
        Assert.False(r.IsSuccess);
        Assert.Equal(tipo, r.Type);
        Assert.Equal(mensaje, r.Error);
    }

    // --- Caso feliz y evento ---

    [Fact]
    public async Task Registrar_Con_Mesa_Guarda_La_Visita_De_Hoy_Y_Publica_El_Evento_Una_Vez_Sin_Reserva()
    {
        var publisher = new PublisherFalso();
        using var e = new Entorno(Utc(13, 0), publisher);
        await e.SembrarAsync();

        var resultado = await e.RegistrarAsync(e.Almuerzo.Id, e.Mesa.Id, 3);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(Dia, resultado.Value!.Fecha);
        Assert.Equal(5, resultado.Value.MesaNumero);
        Assert.Equal("Almuerzo", resultado.Value.FranjaNombre);
        var guardada = await e.Contexto.VisitasSinReserva.SingleAsync();
        Assert.Equal(Dia, guardada.Fecha);
        Assert.Equal(e.Mesa.Id, guardada.MesaId);
        Assert.Equal(3, guardada.PersonasReales);
        var evento = Assert.IsType<VisitaCompletadaEvent>(Assert.Single(publisher.Publicados));
        Assert.Equal(new VisitaCompletadaEvent(Dia, e.Almuerzo.Id, 3, ConReserva: false), evento);
    }

    [Fact]
    public async Task Registrar_Publica_El_Evento_Despues_De_Guardar()
    {
        var cantidadAlPublicar = -1;
        Entorno? entorno = null;
        var publisher = new PublisherQueVerifica(async () =>
        {
            // Un contexto nuevo sobre la misma base: solo ve lo que ya está guardado.
            using var otro = new GiraDbContext(entorno!.Opciones);
            cantidadAlPublicar = await otro.VisitasSinReserva.CountAsync();
        });
        using var e = new Entorno(Utc(13, 0), publisher);
        entorno = e;
        await e.SembrarAsync();

        await e.RegistrarAsync(e.Almuerzo.Id, e.Mesa.Id, 2);

        Assert.Equal(1, cantidadAlPublicar);
        Assert.Single(publisher.Publicados);
    }

    [Fact]
    public async Task Registrar_Sin_Mesa_Guarda_Con_MesaId_Nulo()
    {
        using var e = new Entorno(Utc(13, 0), new PublisherFalso());
        await e.SembrarAsync();

        var resultado = await e.RegistrarAsync(e.Almuerzo.Id, null, 6);

        Assert.True(resultado.IsSuccess);
        Assert.Null(resultado.Value!.MesaId);
        Assert.Null((await e.Contexto.VisitasSinReserva.SingleAsync()).MesaId);
    }

    // --- Personas ---

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task Registrar_Con_Menos_De_1_Persona_Es_Validation(int personas)
    {
        using var e = new Entorno(Utc(13, 0), new PublisherFalso());
        await e.SembrarAsync();

        EsFallo(await e.RegistrarAsync(e.Almuerzo.Id, null, personas), ErrorType.Validation, "Debe haber al menos 1 persona.");
    }

    [Fact]
    public async Task Sin_Mesa_El_Tope_Es_La_Capacidad_Maxima_De_Una_Mesa()
    {
        using var e = new Entorno(Utc(13, 0), new PublisherFalso());
        await e.SembrarAsync();

        var enElTope = await e.RegistrarAsync(e.Almuerzo.Id, null, GuardarMesaRequestValidator.CapacidadMaxima);
        var pasado = await e.RegistrarAsync(e.Almuerzo.Id, null, GuardarMesaRequestValidator.CapacidadMaxima + 1);

        Assert.True(enElTope.IsSuccess);
        EsFallo(pasado, ErrorType.Validation, "Sin mesa, el máximo es 50 personas.");
    }

    [Fact]
    public async Task Con_Mesa_El_Tope_Es_Su_Capacidad()
    {
        using var e = new Entorno(Utc(13, 0), new PublisherFalso());
        await e.SembrarAsync();

        var enLaCapacidad = await e.RegistrarAsync(e.Almuerzo.Id, e.Mesa.Id, 4);
        var pasado = await e.RegistrarAsync(e.Almuerzo.Id, e.Mesa.Id, 5);

        Assert.True(enLaCapacidad.IsSuccess);
        EsFallo(pasado, ErrorType.Validation, "La mesa 5 admite como máximo 4 personas.");
        Assert.Equal(1, await e.Contexto.VisitasSinReserva.CountAsync());
    }

    // --- Franja: activa y ya iniciada hoy ---

    [Fact]
    public async Task En_El_Instante_Exacto_De_Inicio_De_La_Franja_Se_Acepta()
    {
        using var e = new Entorno(Utc(12, 0), new PublisherFalso());
        await e.SembrarAsync();

        Assert.True((await e.RegistrarAsync(e.Almuerzo.Id, null, 2)).IsSuccess);
    }

    [Fact]
    public async Task Un_Minuto_Antes_Del_Inicio_De_La_Franja_Es_Conflict()
    {
        using var e = new Entorno(Utc(11, 59), new PublisherFalso());
        await e.SembrarAsync();

        var resultado = await e.RegistrarAsync(e.Almuerzo.Id, null, 2);

        EsFallo(resultado, ErrorType.Conflict,
            "La franja Almuerzo empieza a las 12:00; todavía no se puede registrar una visita en ella.");
        Assert.Equal(0, await e.Contexto.VisitasSinReserva.CountAsync());
    }

    [Fact]
    public async Task Una_Franja_Futura_Es_Conflict_Aunque_Otra_Ya_Haya_Empezado()
    {
        using var e = new Entorno(Utc(16, 0), new PublisherFalso());
        await e.SembrarAsync();

        var resultado = await e.RegistrarAsync(e.Cena.Id, null, 2);

        EsFallo(resultado, ErrorType.Conflict,
            "La franja Cena empieza a las 18:00; todavía no se puede registrar una visita en ella.");
    }

    [Fact]
    public async Task Una_Franja_Ya_Terminada_Se_Acepta()
    {
        using var e = new Entorno(Utc(16, 0), new PublisherFalso()); // Almuerzo terminó a las 15:00
        await e.SembrarAsync();

        var resultado = await e.RegistrarAsync(e.Almuerzo.Id, null, 2);

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public async Task Una_Franja_Inactiva_Es_Conflict()
    {
        using var e = new Entorno(Utc(13, 0), new PublisherFalso());
        await e.SembrarAsync(almuerzoActivo: false);

        EsFallo(await e.RegistrarAsync(e.Almuerzo.Id, null, 2), ErrorType.Conflict, "La franja indicada no está activa.");
    }

    [Fact]
    public async Task Una_Franja_Inexistente_Es_Validation()
    {
        using var e = new Entorno(Utc(13, 0), new PublisherFalso());
        await e.SembrarAsync();

        EsFallo(await e.RegistrarAsync(Guid.NewGuid(), null, 2), ErrorType.Validation, "La franja indicada no existe.");
    }

    // --- Mesa ---

    [Fact]
    public async Task Una_Mesa_Inexistente_Es_Validation()
    {
        using var e = new Entorno(Utc(13, 0), new PublisherFalso());
        await e.SembrarAsync();

        EsFallo(await e.RegistrarAsync(e.Almuerzo.Id, Guid.NewGuid(), 2), ErrorType.Validation, "La mesa indicada no existe.");
    }

    [Fact]
    public async Task Una_Mesa_Inactiva_Es_Conflict()
    {
        using var e = new Entorno(Utc(13, 0), new PublisherFalso());
        await e.SembrarAsync(mesaActiva: false);

        EsFallo(await e.RegistrarAsync(e.Almuerzo.Id, e.Mesa.Id, 2), ErrorType.Conflict, "La mesa 5 no está activa.");
    }

    [Fact]
    public async Task Una_Mesa_De_Zona_Inactiva_Es_Conflict()
    {
        using var e = new Entorno(Utc(13, 0), new PublisherFalso());
        await e.SembrarAsync(zonaActiva: false);

        EsFallo(await e.RegistrarAsync(e.Almuerzo.Id, e.Mesa.Id, 2), ErrorType.Conflict, "La zona de la mesa 5 no está activa.");
    }

    [Theory]
    [InlineData(EstadoReserva.Confirmada)]
    [InlineData(EstadoReserva.EnCurso)]
    public async Task Una_Mesa_Con_Reserva_Vigente_En_Esa_Franja_Es_Conflict(EstadoReserva estado)
    {
        var publisher = new PublisherFalso();
        using var e = new Entorno(Utc(13, 0), publisher);
        await e.SembrarAsync();
        await e.SembrarReservaAsync(e.Almuerzo, estado);

        var resultado = await e.RegistrarAsync(e.Almuerzo.Id, e.Mesa.Id, 2);

        EsFallo(resultado, ErrorType.Conflict, "La mesa 5 tiene una reserva en esta franja.");
        Assert.Empty(publisher.Publicados);
    }

    [Theory]
    [InlineData(EstadoReserva.Cancelada)]
    [InlineData(EstadoReserva.Completada)]
    public async Task Una_Reserva_Que_Ya_No_Esta_Vigente_No_Bloquea_La_Mesa(EstadoReserva estado)
    {
        using var e = new Entorno(Utc(13, 0), new PublisherFalso());
        await e.SembrarAsync();
        await e.SembrarReservaAsync(e.Almuerzo, estado);

        Assert.True((await e.RegistrarAsync(e.Almuerzo.Id, e.Mesa.Id, 2)).IsSuccess);
    }

    [Fact]
    public async Task Una_Reserva_De_Otra_Franja_No_Bloquea_La_Mesa()
    {
        using var e = new Entorno(Utc(19, 0), new PublisherFalso());
        await e.SembrarAsync();
        await e.SembrarReservaAsync(e.Cena, EstadoReserva.Confirmada);

        Assert.True((await e.RegistrarAsync(e.Almuerzo.Id, e.Mesa.Id, 2)).IsSuccess);
    }

    // --- Handler que falla ---

    [Fact]
    public async Task Un_Handler_Que_Falla_No_Provoca_Error_Ni_Revierte_La_Visita()
    {
        var publisher = new PublisherQueFalla();
        using var e = new Entorno(Utc(13, 0), publisher);
        await e.SembrarAsync();

        var resultado = await e.RegistrarAsync(e.Almuerzo.Id, e.Mesa.Id, 2);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, publisher.Intentos);
        Assert.Equal(1, await e.Contexto.VisitasSinReserva.CountAsync());
    }
}
