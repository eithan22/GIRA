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

        public ParametrosReservaFalso Parametros { get; }

        public Entorno(DateTimeOffset ahoraUtc, IPublisher publisher, NullLogger<PublicadorEventosSeguro>? logger = null)
        {
            Opciones = new DbContextOptionsBuilder<GiraDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            Contexto = new GiraDbContext(Opciones);
            var reloj = new TimeProviderFalso(ahoraUtc);
            Parametros = new ParametrosReservaFalso();

            Servicio = new VisitaService(
                new VisitaRepository(Contexto),
                new FranjaGestionRepository(Contexto),
                new MesaGestionRepository(Contexto),
                new ZonaRepository(Contexto),
                new ReservaRepository(Contexto),
                Parametros,
                new RegistrarVisitaRequestValidator(),
                new ListarVisitasRequestValidator(reloj),
                new PublicadorEventosSeguro(publisher, logger ?? NullLogger<PublicadorEventosSeguro>.Instance),
                reloj);
        }

        public async Task SembrarAsync(bool zonaActiva = true, bool mesaActiva = true, bool almuerzoActivo = true, bool cenaActiva = true)
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
                Id = Guid.NewGuid(), Nombre = "Cena", HoraInicio = new TimeOnly(18, 0), HoraFin = new TimeOnly(22, 30), Activa = cenaActiva
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
    public async Task Registrar_Sin_Mesa_Es_Validation()
    {
        using var e = new Entorno(Utc(13, 0), new PublisherFalso());
        await e.SembrarAsync();

        EsFallo(await e.RegistrarAsync(e.Almuerzo.Id, null, 6), ErrorType.Validation, "Elige la mesa donde se sentó el grupo.");
        Assert.Equal(0, await e.Contexto.VisitasSinReserva.CountAsync());
    }

    [Fact]
    public async Task Registrar_Con_MesaId_Vacio_Es_Validation()
    {
        using var e = new Entorno(Utc(13, 0), new PublisherFalso());
        await e.SembrarAsync();

        EsFallo(await e.RegistrarAsync(e.Almuerzo.Id, Guid.Empty, 6), ErrorType.Validation, "Elige la mesa donde se sentó el grupo.");
    }

    // --- Personas ---

    [Theory]
    [InlineData(0)]
    [InlineData(-2)]
    public async Task Registrar_Con_Menos_De_1_Persona_Es_Validation(int personas)
    {
        using var e = new Entorno(Utc(13, 0), new PublisherFalso());
        await e.SembrarAsync();

        EsFallo(await e.RegistrarAsync(e.Almuerzo.Id, e.Mesa.Id, personas), ErrorType.Validation, "Debe haber al menos 1 persona.");
    }

    [Fact]
    public async Task En_La_Capacidad_Exacta_De_La_Mesa_Se_Acepta()
    {
        using var e = new Entorno(Utc(13, 0), new PublisherFalso());
        await e.SembrarAsync();

        var enLaCapacidad = await e.RegistrarAsync(e.Almuerzo.Id, e.Mesa.Id, 4);

        Assert.True(enLaCapacidad.IsSuccess);
        Assert.Equal(1, await e.Contexto.VisitasSinReserva.CountAsync());
    }

    [Fact]
    public async Task Pasado_El_Tope_De_La_Mesa_Es_Validation()
    {
        // Con una mesa sin ninguna visita todavía: si ya tuviera una (como antes, encadenado con la prueba
        // de arriba), la segunda llamada chocaría primero con R3 (la mesa ya tiene una visita), no con el
        // tope de personas. R3 va antes que la capacidad (se agrega después del chequeo de reserva vigente).
        using var e = new Entorno(Utc(13, 0), new PublisherFalso());
        await e.SembrarAsync();

        var pasado = await e.RegistrarAsync(e.Almuerzo.Id, e.Mesa.Id, 5);

        EsFallo(pasado, ErrorType.Validation, "La mesa 5 admite como máximo 4 personas.");
        Assert.Equal(0, await e.Contexto.VisitasSinReserva.CountAsync());
    }

    // --- Franja: activa y ya iniciada hoy ---

    [Fact]
    public async Task En_El_Instante_Exacto_De_Inicio_De_La_Franja_Se_Acepta()
    {
        using var e = new Entorno(Utc(12, 0), new PublisherFalso());
        await e.SembrarAsync();

        Assert.True((await e.RegistrarAsync(e.Almuerzo.Id, e.Mesa.Id, 2)).IsSuccess);
    }

    [Fact]
    public async Task Un_Minuto_Antes_Del_Inicio_De_La_Franja_Es_Conflict()
    {
        // Antes eran las 11:59 con el Almuerzo: ahora a esa hora el restaurante está cerrado (abre a las 12:00) y
        // el mensaje es el del horario (ver Antes_De_La_Apertura_...). Se prueba con la Cena a las 17:59, con el
        // restaurante abierto y el Almuerzo ya empezado.
        using var e = new Entorno(Utc(17, 59), new PublisherFalso());
        await e.SembrarAsync();

        var resultado = await e.RegistrarAsync(e.Cena.Id, e.Mesa.Id, 2);

        EsFallo(resultado, ErrorType.Conflict,
            "La franja Cena empieza a las 18:00; todavía no se puede registrar una visita en ella.");
        Assert.Equal(0, await e.Contexto.VisitasSinReserva.CountAsync());
    }

    [Fact]
    public async Task Una_Franja_Futura_Es_Conflict_Aunque_Otra_Ya_Haya_Empezado()
    {
        using var e = new Entorno(Utc(16, 0), new PublisherFalso());
        await e.SembrarAsync();

        var resultado = await e.RegistrarAsync(e.Cena.Id, e.Mesa.Id, 2);

        EsFallo(resultado, ErrorType.Conflict,
            "La franja Cena empieza a las 18:00; todavía no se puede registrar una visita en ella.");
    }

    [Fact]
    public async Task Entre_Franjas_Se_Acepta_La_Ultima_Que_Empezo_Aunque_Ya_Haya_Terminado()
    {
        // Antes: "Una_Franja_Ya_Terminada_Se_Acepta" (se aceptaba cualquier franja que ya hubiera empezado).
        // Ahora solo se acepta la más reciente que empezó; con el Almuerzo (12:00–15:00) y la Cena (18:00) a las
        // 16:00 la última que empezó es el Almuerzo.
        using var e = new Entorno(Utc(16, 0), new PublisherFalso()); // Almuerzo terminó a las 15:00
        await e.SembrarAsync();

        var resultado = await e.RegistrarAsync(e.Almuerzo.Id, e.Mesa.Id, 2);

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public async Task Una_Franja_Inactiva_Es_Conflict()
    {
        using var e = new Entorno(Utc(13, 0), new PublisherFalso());
        await e.SembrarAsync(almuerzoActivo: false);

        EsFallo(await e.RegistrarAsync(e.Almuerzo.Id, e.Mesa.Id, 2), ErrorType.Conflict, "La franja indicada no está activa.");
    }

    [Fact]
    public async Task Una_Franja_Inexistente_Es_Validation()
    {
        using var e = new Entorno(Utc(13, 0), new PublisherFalso());
        await e.SembrarAsync();

        EsFallo(await e.RegistrarAsync(Guid.NewGuid(), e.Mesa.Id, 2), ErrorType.Validation, "La franja indicada no existe.");
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
        // Antes el reloj marcaba las 19:00 y se registraba en el Almuerzo (ya anterior): la regla nueva exige la franja
        // más reciente, así que se prueba a las 13:00, con el Almuerzo vigente y la reserva en la Cena (otra franja).
        using var e = new Entorno(Utc(13, 0), new PublisherFalso());
        await e.SembrarAsync();
        await e.SembrarReservaAsync(e.Cena, EstadoReserva.Confirmada);

        Assert.True((await e.RegistrarAsync(e.Almuerzo.Id, e.Mesa.Id, 2)).IsSuccess);
    }

    // --- Una mesa por franja (R3 del ajuste de visitas, GI-166) ---

    [Fact]
    public async Task Una_Mesa_Con_Otra_Visita_En_Esa_Fecha_Y_Franja_Es_Conflict()
    {
        var publisher = new PublisherFalso();
        using var e = new Entorno(Utc(13, 0), publisher);
        await e.SembrarAsync();
        Assert.True((await e.RegistrarAsync(e.Almuerzo.Id, e.Mesa.Id, 2)).IsSuccess);

        var segunda = await e.RegistrarAsync(e.Almuerzo.Id, e.Mesa.Id, 3);

        EsFallo(segunda, ErrorType.Conflict, "La mesa 5 ya tiene una visita en esta franja.");
        Assert.Equal(1, await e.Contexto.VisitasSinReserva.CountAsync());
        Assert.Single(publisher.Publicados); // solo la primera visita publicó el evento
    }

    [Fact]
    public async Task La_Misma_Mesa_En_Otra_Franja_Se_Acepta()
    {
        using var e = new Entorno(Utc(13, 0), new PublisherFalso());
        await e.SembrarAsync();
        Assert.True((await e.RegistrarAsync(e.Almuerzo.Id, e.Mesa.Id, 2)).IsSuccess);

        // Mismo contexto (misma mesa, misma base en memoria) con el reloj adelantado a la Cena: otra
        // franja, misma mesa; no debe chocar con la visita que ya existe en el Almuerzo.
        var servicioEnCena = new VisitaService(
            new VisitaRepository(e.Contexto), new FranjaGestionRepository(e.Contexto), new MesaGestionRepository(e.Contexto),
            new ZonaRepository(e.Contexto), new ReservaRepository(e.Contexto), e.Parametros,
            new RegistrarVisitaRequestValidator(), new ListarVisitasRequestValidator(new TimeProviderFalso(Utc(19, 0))),
            new PublicadorEventosSeguro(new PublisherFalso(), NullLogger<PublicadorEventosSeguro>.Instance),
            new TimeProviderFalso(Utc(19, 0)));

        var resultado = await servicioEnCena.RegistrarAsync(new RegistrarVisitaRequest(e.Cena.Id, e.Mesa.Id, 2), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(2, await e.Contexto.VisitasSinReserva.CountAsync());
    }

    [Fact]
    public async Task Otra_Mesa_En_La_Misma_Franja_Se_Acepta()
    {
        using var e = new Entorno(Utc(13, 0), new PublisherFalso());
        await e.SembrarAsync();
        var otraMesa = new Mesa { Id = Guid.NewGuid(), Numero = 6, Capacidad = 4, ZonaId = e.Zona.Id, Activa = true };
        e.Contexto.Mesas.Add(otraMesa);
        await e.Contexto.SaveChangesAsync();
        Assert.True((await e.RegistrarAsync(e.Almuerzo.Id, e.Mesa.Id, 2)).IsSuccess);

        var resultado = await e.RegistrarAsync(e.Almuerzo.Id, otraMesa.Id, 2);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(2, await e.Contexto.VisitasSinReserva.CountAsync());
    }

    [Fact]
    public async Task Una_Visita_Nueva_Deja_La_Mesa_Con_TieneVisita_En_El_Mapa()
    {
        using var e = new Entorno(Utc(13, 0), new PublisherFalso());
        await e.SembrarAsync();

        await e.RegistrarAsync(e.Almuerzo.Id, e.Mesa.Id, 2);

        // Misma consulta que usa OcupacionService para pintar «Con visita» en el mapa (no se duplica lógica).
        var mesasConVisita = await new VisitaRepository(e.Contexto).ListarMesasConVisitaAsync(Dia, e.Almuerzo.Id, default);
        Assert.Contains(e.Mesa.Id, mesasConVisita);
    }

    // --- Restaurante abierto ahora (el cierre es exclusivo, igual que el fin de una franja) ---

    [Fact]
    public async Task Con_El_Dia_De_La_Semana_Cerrado_Es_Conflict()
    {
        using var e = new Entorno(Utc(13, 0), new PublisherFalso()); // 1-dic-2026 es martes
        e.Parametros.DiasCerrados = new HashSet<DayOfWeek> { DayOfWeek.Tuesday };
        await e.SembrarAsync();

        var resultado = await e.RegistrarAsync(e.Almuerzo.Id, e.Mesa.Id, 2);

        EsFallo(resultado, ErrorType.Conflict, "El restaurante está cerrado hoy; no se pueden registrar visitas sin reserva.");
        Assert.Equal(0, await e.Contexto.VisitasSinReserva.CountAsync());
    }

    [Fact]
    public async Task Con_La_Fecha_De_Hoy_Cerrada_Es_Conflict()
    {
        using var e = new Entorno(Utc(13, 0), new PublisherFalso());
        e.Parametros.FechasCerradas = new HashSet<DateOnly> { Dia };
        await e.SembrarAsync();

        var resultado = await e.RegistrarAsync(e.Almuerzo.Id, e.Mesa.Id, 2);

        EsFallo(resultado, ErrorType.Conflict, "El restaurante está cerrado hoy; no se pueden registrar visitas sin reserva.");
    }

    [Fact]
    public async Task Antes_De_La_Apertura_Es_Conflict_Con_El_Horario_Del_Dia()
    {
        using var e = new Entorno(Utc(11, 59), new PublisherFalso()); // abre a las 12:00
        await e.SembrarAsync();

        var resultado = await e.RegistrarAsync(e.Almuerzo.Id, e.Mesa.Id, 2);

        EsFallo(resultado, ErrorType.Conflict,
            "El restaurante está cerrado ahora; el horario de hoy es de 12:00 a 22:30. " +
            "No se pueden registrar visitas sin reserva fuera de horario.");
    }

    [Fact]
    public async Task Despues_Del_Cierre_Es_Conflict_Aunque_La_Franja_Ya_Haya_Empezado()
    {
        // Caso real de la prueba: 23:26 con el restaurante cerrando a las 22:30.
        using var e = new Entorno(Utc(23, 26), new PublisherFalso());
        await e.SembrarAsync();

        var resultado = await e.RegistrarAsync(e.Cena.Id, e.Mesa.Id, 2);

        EsFallo(resultado, ErrorType.Conflict,
            "El restaurante está cerrado ahora; el horario de hoy es de 12:00 a 22:30. " +
            "No se pueden registrar visitas sin reserva fuera de horario.");
        Assert.Equal(0, await e.Contexto.VisitasSinReserva.CountAsync());
    }

    [Fact]
    public async Task En_El_Instante_Exacto_Del_Cierre_Es_Conflict_Y_Un_Minuto_Antes_Se_Acepta()
    {
        using var cierre = new Entorno(Utc(22, 30), new PublisherFalso());
        await cierre.SembrarAsync();
        Assert.False((await cierre.RegistrarAsync(cierre.Cena.Id, cierre.Mesa.Id, 2)).IsSuccess);

        using var antes = new Entorno(Utc(22, 29), new PublisherFalso());
        await antes.SembrarAsync();
        Assert.True((await antes.RegistrarAsync(antes.Cena.Id, antes.Mesa.Id, 2)).IsSuccess);
    }

    [Fact]
    public async Task Con_Un_Horario_Editado_Se_Usa_El_Horario_Configurado()
    {
        using var e = new Entorno(Utc(21, 0), new PublisherFalso());
        e.Parametros.HoraCierre = new TimeOnly(20, 0); // el restaurante cierra a las 20:00
        await e.SembrarAsync();

        var resultado = await e.RegistrarAsync(e.Cena.Id, e.Mesa.Id, 2);

        EsFallo(resultado, ErrorType.Conflict,
            "El restaurante está cerrado ahora; el horario de hoy es de 12:00 a 20:00. " +
            "No se pueden registrar visitas sin reserva fuera de horario.");
    }

    // --- Franja más reciente de las que ya empezaron ---

    [Fact]
    public async Task Una_Franja_Anterior_Es_Conflict_Y_El_Mensaje_Nombra_La_Franja_Correcta()
    {
        using var e = new Entorno(Utc(19, 0), new PublisherFalso()); // la Cena (18:00) ya empezó
        await e.SembrarAsync();

        var resultado = await e.RegistrarAsync(e.Almuerzo.Id, e.Mesa.Id, 2);

        EsFallo(resultado, ErrorType.Conflict,
            "Esa franja ya no es la actual: la visita se registra en la franja Cena (18:00–22:30).");
        Assert.Equal(0, await e.Contexto.VisitasSinReserva.CountAsync());
    }

    [Fact]
    public async Task La_Franja_Actual_Se_Acepta_Cuando_Hay_Una_Anterior()
    {
        using var e = new Entorno(Utc(19, 0), new PublisherFalso());
        await e.SembrarAsync();

        Assert.True((await e.RegistrarAsync(e.Cena.Id, e.Mesa.Id, 2)).IsSuccess);
    }

    [Fact]
    public async Task Entre_Tres_Franjas_Se_Acepta_Solo_La_Ultima_Empezada_Y_Se_Rechaza_La_Anterior()
    {
        using var e = new Entorno(Utc(17, 0), new PublisherFalso()); // entre la Tarde (15:30–16:30) y la Cena (18:00)
        await e.SembrarAsync();
        var tarde = new FranjaHoraria
        {
            Id = Guid.NewGuid(), Nombre = "Tarde", HoraInicio = new TimeOnly(15, 30), HoraFin = new TimeOnly(16, 30)
        };
        e.Contexto.FranjasHorarias.Add(tarde);
        await e.Contexto.SaveChangesAsync();

        EsFallo(await e.RegistrarAsync(e.Almuerzo.Id, e.Mesa.Id, 2), ErrorType.Conflict,
            "Esa franja ya no es la actual: la visita se registra en la franja Tarde (15:30–16:30).");
        Assert.True((await e.RegistrarAsync(tarde.Id, e.Mesa.Id, 2)).IsSuccess);
        EsFallo(await e.RegistrarAsync(e.Cena.Id, e.Mesa.Id, 2), ErrorType.Conflict,
            "La franja Cena empieza a las 18:00; todavía no se puede registrar una visita en ella.");
    }

    [Fact]
    public async Task Una_Franja_Inactiva_No_Cuenta_Como_La_Mas_Reciente()
    {
        using var e = new Entorno(Utc(19, 0), new PublisherFalso());
        await e.SembrarAsync(cenaActiva: false); // la Cena (18:00) está inactiva: la última activa que empezó es el Almuerzo

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
