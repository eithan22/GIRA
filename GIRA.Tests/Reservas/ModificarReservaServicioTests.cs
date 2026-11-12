using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Events.Reservas;
using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Application.Services.Reservas;
using GIRA.Application.Validations.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Domain.Exceptions.Reservas;
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
/// Pruebas de la modificación por el personal (GI-160, GI-186): revalidación como reserva nueva
/// (RN-RES-05) sin contar la propia reserva, límite en el inicio de la franja original (RN-RES-04),
/// asignación de mesa, estados, evento y errores. Repositorios reales sobre EF Core InMemory.
/// </summary>
public class ModificarReservaServicioTests
{
    // Reserva original: 5 de diciembre de 2026, Almuerzo (12:00-15:00), mesa 2 (capacidad 4), 2 personas.
    // Mesas: 1 (cap. 2), 2 (cap. 4), 3 (cap. 6), 4 (cap. 4, inactiva). Cena 18:00-22:30.
    private static readonly DateOnly Original = new(2026, 12, 5);
    private static readonly DateOnly Otro = new(2026, 12, 6); // domingo

    private static DateTimeOffset Utc(int dia, int hora, int minuto) =>
        new(new DateTime(2026, 12, dia, hora, minuto, 0).AddHours(4), TimeSpan.Zero);

    private sealed class Entorno : IDisposable
    {
        public DbContextOptions<GiraDbContext> Opciones { get; }
        public GiraDbContext Contexto { get; }
        public ReservaService Servicio { get; }
        public ParametrosReservaFalso Parametros { get; } = new();
        public PublisherFalso Publisher { get; } = new();
        public FranjaHoraria Almuerzo { get; private set; } = null!;
        public FranjaHoraria Cena { get; private set; } = null!;
        public Mesa Mesa1 { get; private set; } = null!;
        public Mesa Mesa2 { get; private set; } = null!;
        public Mesa Mesa3 { get; private set; } = null!;
        public Mesa Mesa4Inactiva { get; private set; } = null!;
        public Reserva Reserva { get; private set; } = null!;
        public Cliente Cliente { get; private set; } = null!;

        public Entorno(
            DateTimeOffset ahoraUtc, IPublisher? publisher = null, ILogger<PublicadorEventosSeguro>? logger = null,
            Func<IReservaRepository, IReservaRepository>? envolver = null)
        {
            Opciones = new DbContextOptionsBuilder<GiraDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            Contexto = new GiraDbContext(Opciones);

            var reloj = new TimeProviderFalso(ahoraUtc);
            IReservaRepository reservas = new ReservaRepository(Contexto);
            if (envolver is not null)
            {
                reservas = envolver(reservas);
            }
            publisher ??= Publisher;

            Servicio = new ReservaService(
                new MesaRepository(Contexto),
                new FranjaHorariaRepository(Contexto),
                reservas,
                new ClienteRepository(Contexto),
                new MesaGestionRepository(Contexto),
                Parametros,
                new CrearReservaRequestValidator(Parametros, reloj),
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
            EstadoReserva estado = EstadoReserva.Confirmada, DateOnly? fecha = null, bool enCena = false)
        {
            var zona = new Zona { Id = Guid.NewGuid(), Nombre = "Salón" };
            Almuerzo = new FranjaHoraria
            {
                Id = Guid.NewGuid(), Nombre = "Almuerzo", HoraInicio = new TimeOnly(12, 0), HoraFin = new TimeOnly(15, 0)
            };
            Cena = new FranjaHoraria
            {
                Id = Guid.NewGuid(), Nombre = "Cena", HoraInicio = new TimeOnly(18, 0), HoraFin = new TimeOnly(22, 30)
            };
            Mesa1 = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2, ZonaId = zona.Id };
            Mesa2 = new Mesa { Id = Guid.NewGuid(), Numero = 2, Capacidad = 4, ZonaId = zona.Id };
            Mesa3 = new Mesa { Id = Guid.NewGuid(), Numero = 3, Capacidad = 6, ZonaId = zona.Id };
            Mesa4Inactiva = new Mesa { Id = Guid.NewGuid(), Numero = 4, Capacidad = 4, ZonaId = zona.Id, Activa = false };
            Cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Cliente Ejemplo", Telefono = "8095550101" };
            Reserva = new Reserva
            {
                Id = Guid.NewGuid(), Codigo = "10000001", ClienteId = Cliente.Id, MesaId = Mesa2.Id,
                FranjaHorariaId = (enCena ? Cena : Almuerzo).Id, Fecha = fecha ?? Original, Personas = 2,
                Origen = OrigenReserva.Telefono
            };
            switch (estado)
            {
                case EstadoReserva.EnCurso:
                    Reserva.HacerCheckIn(2, DateTime.UtcNow);
                    break;
                case EstadoReserva.Completada:
                    Reserva.HacerCheckIn(2, DateTime.UtcNow);
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
            Contexto.FranjasHorarias.AddRange(Almuerzo, Cena);
            Contexto.Mesas.AddRange(Mesa1, Mesa2, Mesa3, Mesa4Inactiva);
            Contexto.Clientes.Add(Cliente);
            Contexto.Reservas.Add(Reserva);
            await Contexto.SaveChangesAsync();
        }

        public async Task SembrarOtraReservaAsync(Mesa mesa, FranjaHoraria franja, DateOnly fecha)
        {
            Contexto.Reservas.Add(new Reserva
            {
                Id = Guid.NewGuid(), Codigo = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant(), ClienteId = Cliente.Id,
                MesaId = mesa.Id, FranjaHorariaId = franja.Id, Fecha = fecha, Personas = 2, Origen = OrigenReserva.Telefono
            });
            await Contexto.SaveChangesAsync();
        }

        public Task<OperationResult<ReservaDetalleDto>> ModificarAsync(
            DateOnly? fecha = null, Guid? franjaId = null, int personas = 2, Guid? mesaId = null) =>
            Servicio.ModificarAsync(
                Reserva.Id, new ModificarReservaRequest(fecha ?? Original, franjaId ?? Almuerzo.Id, personas, mesaId), default);

        public Task<Reserva> LeerAsync() => Contexto.Reservas.AsNoTracking().SingleAsync(r => r.Id == Reserva.Id);

        public void Dispose() => Contexto.Dispose();
    }

    private static void EsFallo(OperationResult<ReservaDetalleDto> r, ErrorType tipo, string mensaje)
    {
        Assert.False(r.IsSuccess);
        Assert.Equal(tipo, r.Type);
        Assert.Equal(mensaje, r.Error);
    }

    // --- Cambios ---

    [Fact]
    public async Task Cambiar_Solo_Las_Personas_Conserva_La_Mesa_Y_No_Choca_Consigo_Misma()
    {
        using var e = new Entorno(Utc(1, 10, 0));
        await e.SembrarAsync();

        var resultado = await e.ModificarAsync(personas: 4); // mesa 2 (cap. 4) sigue sirviendo

        Assert.True(resultado.IsSuccess);
        Assert.Equal(4, resultado.Value!.Personas);
        Assert.Equal(e.Mesa2.Id, resultado.Value.MesaId);
        Assert.Equal("Confirmada", resultado.Value.Estado);
    }

    [Fact]
    public async Task Si_La_Mesa_Actual_Ya_No_Cabe_Se_Asigna_La_Menor_Libre_Que_Alcance()
    {
        using var e = new Entorno(Utc(1, 10, 0));
        await e.SembrarAsync();

        var resultado = await e.ModificarAsync(personas: 5); // solo la mesa 3 (cap. 6) alcanza

        Assert.True(resultado.IsSuccess);
        Assert.Equal(e.Mesa3.Id, resultado.Value!.MesaId);
    }

    [Fact]
    public async Task Cambiar_De_Fecha_Conserva_La_Mesa_Si_Esta_Libre()
    {
        using var e = new Entorno(Utc(1, 10, 0));
        await e.SembrarAsync();

        var resultado = await e.ModificarAsync(fecha: Otro);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(Otro, resultado.Value!.Fecha);
        Assert.Equal(e.Mesa2.Id, resultado.Value.MesaId);
    }

    [Fact]
    public async Task Cambiar_De_Franja_Con_La_Mesa_Ocupada_Reasigna_La_Menor_Libre()
    {
        using var e = new Entorno(Utc(1, 10, 0));
        await e.SembrarAsync();
        await e.SembrarOtraReservaAsync(e.Mesa2, e.Cena, Original); // la mesa 2 ya está tomada en la Cena

        var resultado = await e.ModificarAsync(franjaId: e.Cena.Id);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(e.Mesa1.Id, resultado.Value!.MesaId); // 2 personas: la menor libre es la mesa 1
        Assert.Equal(e.Cena.Id, resultado.Value.FranjaId);
    }

    [Fact]
    public async Task Una_Mesa_Elegida_Libre_Se_Asigna()
    {
        using var e = new Entorno(Utc(1, 10, 0));
        await e.SembrarAsync();

        var resultado = await e.ModificarAsync(mesaId: e.Mesa3.Id);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(e.Mesa3.Id, resultado.Value!.MesaId);
    }

    [Fact]
    public async Task Una_Mesa_Elegida_Ocupada_Es_Conflict_Y_No_Cambia_La_Reserva()
    {
        using var e = new Entorno(Utc(1, 10, 0));
        await e.SembrarAsync();
        await e.SembrarOtraReservaAsync(e.Mesa3, e.Almuerzo, Original);

        var resultado = await e.ModificarAsync(mesaId: e.Mesa3.Id);

        EsFallo(resultado, ErrorType.Conflict, "La mesa 3 ya está reservada en esa fecha y franja.");
        Assert.Equal(e.Mesa2.Id, (await e.LeerAsync()).MesaId);
    }

    [Fact]
    public async Task Una_Mesa_Elegida_Que_No_Cabe_Es_Validation()
    {
        using var e = new Entorno(Utc(1, 10, 0));
        await e.SembrarAsync();

        var resultado = await e.ModificarAsync(personas: 3, mesaId: e.Mesa1.Id);

        EsFallo(resultado, ErrorType.Validation, "La mesa 1 admite como máximo 2 personas.");
    }

    [Fact]
    public async Task Una_Mesa_Elegida_Inactiva_Es_Conflict()
    {
        using var e = new Entorno(Utc(1, 10, 0));
        await e.SembrarAsync();

        var resultado = await e.ModificarAsync(mesaId: e.Mesa4Inactiva.Id);

        EsFallo(resultado, ErrorType.Conflict, "La mesa 4 no está disponible: la mesa o su zona están inactivas.");
    }

    [Fact]
    public async Task Una_Mesa_Elegida_Inexistente_Es_Validation()
    {
        using var e = new Entorno(Utc(1, 10, 0));
        await e.SembrarAsync();

        EsFallo(await e.ModificarAsync(mesaId: Guid.NewGuid()), ErrorType.Validation, "La mesa indicada no existe.");
    }

    // --- Revalidación como reserva nueva (RN-RES-05) ---

    [Fact]
    public async Task Un_Grupo_Mayor_A_La_Mesa_Mas_Grande_Es_Validation()
    {
        using var e = new Entorno(Utc(1, 10, 0));
        await e.SembrarAsync();

        var resultado = await e.ModificarAsync(personas: 7);

        EsFallo(
            resultado, ErrorType.Validation,
            "Las mesas admiten como máximo 6 personas. Los grupos más grandes se atienden por teléfono, fuera del sistema.");
    }

    [Fact]
    public async Task Un_Dia_Cerrado_Es_Validation()
    {
        using var e = new Entorno(Utc(1, 10, 0));
        await e.SembrarAsync();
        e.Parametros.DiasCerrados = new HashSet<DayOfWeek> { Otro.DayOfWeek };

        EsFallo(await e.ModificarAsync(fecha: Otro), ErrorType.Validation, "El restaurante está cerrado ese día.");
    }

    [Fact]
    public async Task Una_Franja_Inexistente_O_Inactiva_Es_Validation()
    {
        using var e = new Entorno(Utc(1, 10, 0));
        await e.SembrarAsync();

        EsFallo(await e.ModificarAsync(franjaId: Guid.NewGuid()), ErrorType.Validation, "La franja horaria no existe o no está activa.");
    }

    [Fact]
    public async Task Una_Fecha_En_El_Pasado_O_Fuera_De_Ventana_Es_Validation_Si_Cambia_El_Horario()
    {
        using var e = new Entorno(Utc(1, 10, 0));
        await e.SembrarAsync();

        EsFallo(await e.ModificarAsync(fecha: new DateOnly(2026, 11, 30)), ErrorType.Validation, "La fecha no puede ser en el pasado.");
        EsFallo(await e.ModificarAsync(fecha: new DateOnly(2027, 1, 15)), ErrorType.Validation, "La fecha supera la ventana máxima de anticipación.");
    }

    [Fact]
    public async Task El_Personal_Puede_Cambiar_A_Una_Franja_De_Hoy_Sin_La_Antelacion_Minima()
    {
        // GI-403: ahora 10:30 del 1 de dic.: la reserva está hoy en la Cena (18:00) y se pasa al Almuerzo
        // (12:00, a menos de 2 h pero sin terminar): el personal no tiene antelación mínima.
        using var e = new Entorno(Utc(1, 10, 30));
        await e.SembrarAsync(fecha: new DateOnly(2026, 12, 1), enCena: true);

        var resultado = await e.ModificarAsync(fecha: new DateOnly(2026, 12, 1), franjaId: e.Almuerzo.Id);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(e.Almuerzo.Id, resultado.Value!.FranjaId);
    }

    [Fact]
    public async Task Cambiar_A_Una_Franja_De_Hoy_Ya_Terminada_Falla()
    {
        // Ahora 16:00: la Cena (18:00) no ha empezado, pero el Almuerzo (12:00-15:00) ya terminó.
        using var e = new Entorno(Utc(1, 16, 0));
        await e.SembrarAsync(fecha: new DateOnly(2026, 12, 1), enCena: true);

        var resultado = await e.ModificarAsync(fecha: new DateOnly(2026, 12, 1), franjaId: e.Almuerzo.Id);

        EsFallo(resultado, ErrorType.Validation, "Esa franja ya terminó; elige una que aún no haya terminado.");
    }

    [Fact]
    public async Task Si_Solo_Cambian_Las_Personas_No_Se_Exige_La_Antelacion_Minima()
    {
        // Ahora 11:00 y la franja original (hoy 12:00) empieza en menos de 2 h, pero todavía no empezó.
        using var e = new Entorno(Utc(1, 11, 0));
        await e.SembrarAsync(fecha: new DateOnly(2026, 12, 1));

        var resultado = await e.ModificarAsync(fecha: new DateOnly(2026, 12, 1), personas: 3);

        Assert.True(resultado.IsSuccess);
    }

    // --- Límite: hasta el inicio de la franja original (RN-RES-04) ---

    [Fact]
    public async Task Un_Minuto_Antes_Del_Inicio_De_La_Franja_Original_Se_Puede_Modificar()
    {
        using var e = new Entorno(Utc(5, 11, 59));
        await e.SembrarAsync();

        Assert.True((await e.ModificarAsync(personas: 3)).IsSuccess);
    }

    [Fact]
    public async Task Exactamente_En_El_Inicio_De_La_Franja_Original_Es_Conflict()
    {
        using var e = new Entorno(Utc(5, 12, 0));
        await e.SembrarAsync();

        var resultado = await e.ModificarAsync(personas: 3);

        EsFallo(resultado, ErrorType.Conflict, "La franja de esta reserva ya empezó; no se puede modificar.");
        Assert.Equal(2, (await e.LeerAsync()).Personas);
    }

    // --- Estados ---

    [Theory]
    [InlineData(EstadoReserva.EnCurso, "Esta reserva ya tiene el check-in registrado; no se puede modificar.")]
    [InlineData(EstadoReserva.Cancelada, "La reserva fue cancelada; no se puede modificar.")]
    [InlineData(EstadoReserva.NoAsistio, "La reserva fue marcada como no asistida; no se puede modificar.")]
    [InlineData(EstadoReserva.Completada, "La reserva ya fue completada; no se puede modificar.")]
    public async Task Modificar_Fuera_De_Estado_Confirmada_Es_Conflict_Con_El_Mensaje_Del_Estado_Y_No_Publica(
        EstadoReserva estado, string mensaje)
    {
        using var e = new Entorno(Utc(1, 10, 0));
        await e.SembrarAsync(estado);

        var resultado = await e.ModificarAsync(personas: 3);

        EsFallo(resultado, ErrorType.Conflict, mensaje);
        Assert.Empty(e.Publisher.Publicados);
    }

    // --- Validación y 404 ---

    [Fact]
    public async Task Datos_Invalidos_Son_Validation()
    {
        using var e = new Entorno(Utc(1, 10, 0));
        await e.SembrarAsync();

        EsFallo(await e.ModificarAsync(personas: 0), ErrorType.Validation, "Debe haber al menos 1 persona.");
        EsFallo(await e.ModificarAsync(franjaId: Guid.Empty), ErrorType.Validation, "La franja es obligatoria.");
    }

    [Fact]
    public async Task Una_Reserva_Inexistente_Es_NotFound()
    {
        using var e = new Entorno(Utc(1, 10, 0));
        await e.SembrarAsync();

        var resultado = await e.Servicio.ModificarAsync(Guid.NewGuid(), new ModificarReservaRequest(Original, e.Almuerzo.Id, 2), default);

        EsFallo(resultado, ErrorType.NotFound, "No encontramos esa reserva.");
    }

    // --- Evento (SRS 3.4: fecha y franja anteriores y nuevas, y personas) ---

    [Fact]
    public async Task Modificar_Publica_El_Evento_Una_Vez_Con_Anteriores_Nuevas_Y_Personas()
    {
        using var e = new Entorno(Utc(1, 10, 0));
        await e.SembrarAsync();

        await e.ModificarAsync(fecha: Otro, franjaId: e.Cena.Id, personas: 3);

        var evento = Assert.IsType<ReservaModificadaEvent>(Assert.Single(e.Publisher.Publicados));
        Assert.Equal(new ReservaModificadaEvent(e.Reserva.Id, Original, e.Almuerzo.Id, Otro, e.Cena.Id, 3), evento);
        Assert.Equal(
            ["ReservaId", "FechaAnterior", "FranjaHorariaIdAnterior", "FechaNueva", "FranjaHorariaIdNueva", "Personas"],
            typeof(ReservaModificadaEvent).GetProperties().Select(p => p.Name).ToArray());
    }

    [Fact]
    public async Task Modificar_Publica_El_Evento_Despues_De_Guardar()
    {
        int? personasAlPublicar = null;
        Entorno? entorno = null;
        var publisher = new PublisherQueVerifica(async () =>
        {
            // Un contexto nuevo sobre la misma base: solo ve lo que ya está guardado.
            using var otro = new GiraDbContext(entorno!.Opciones);
            personasAlPublicar = (await otro.Reservas.SingleAsync()).Personas;
        });
        using var e = new Entorno(Utc(1, 10, 0), publisher);
        entorno = e;
        await e.SembrarAsync();

        await e.ModificarAsync(personas: 4);

        Assert.Equal(4, personasAlPublicar);
        Assert.Single(publisher.Publicados);
    }

    [Fact]
    public async Task Sin_Cambios_Responde_Exito_Sin_Guardar_Ni_Publicar()
    {
        using var e = new Entorno(Utc(1, 10, 0));
        await e.SembrarAsync();

        var resultado = await e.ModificarAsync(); // mismos fecha, franja, personas y mesa

        Assert.True(resultado.IsSuccess);
        Assert.Empty(e.Publisher.Publicados);
        Assert.Null((await e.LeerAsync()).UpdatedAt);
    }

    [Fact]
    public async Task Si_Falla_La_Modificacion_No_Se_Publica_Ningun_Evento()
    {
        using var e = new Entorno(Utc(1, 10, 0));
        await e.SembrarAsync();

        await e.ModificarAsync(personas: 7);
        await e.ModificarAsync(fecha: new DateOnly(2026, 11, 30));

        Assert.Empty(e.Publisher.Publicados);
    }

    [Fact]
    public async Task Un_Handler_Que_Falla_No_Provoca_Error_Ni_Revierte()
    {
        var publisher = new PublisherQueFalla();
        var logger = new LoggerCapturador<PublicadorEventosSeguro>();
        using var e = new Entorno(Utc(1, 10, 0), publisher, logger);
        await e.SembrarAsync();

        var resultado = await e.ModificarAsync(personas: 4);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(1, publisher.Intentos);
        Assert.Equal(4, (await e.LeerAsync()).Personas);
        var entrada = Assert.Single(logger.Entradas);
        Assert.Equal(LogLevel.Error, entrada.Nivel);
        Assert.Contains("ReservaModificadaEvent", entrada.Mensaje);
        Assert.Contains($"ReservaId={e.Reserva.Id}", entrada.Mensaje);
    }

    // --- Concurrencia y carrera por la mesa ---

    [Fact]
    public async Task Con_Conflicto_De_Concurrencia_Es_Conflict_Con_Mensaje_Propio_Y_No_Publica()
    {
        using var e = new Entorno(Utc(1, 10, 0), envolver: r => new ReservaRepositoryConConflicto(r));
        await e.SembrarAsync();

        var resultado = await e.ModificarAsync(personas: 4);

        EsFallo(resultado, ErrorType.Conflict, "La reserva cambió mientras se modificaba; vuelve a consultarla.");
        Assert.Empty(e.Publisher.Publicados);
    }

    [Fact]
    public async Task Si_Otra_Reserva_Toma_La_Mesa_Justo_Antes_De_Guardar_Es_Conflict()
    {
        using var e = new Entorno(Utc(1, 10, 0), envolver: r => new ReservaRepositoryConDuplicado(r));
        await e.SembrarAsync();

        var resultado = await e.ModificarAsync(personas: 4);

        EsFallo(resultado, ErrorType.Conflict, "Esa mesa ya fue tomada, intenta otra franja.");
        Assert.Empty(e.Publisher.Publicados);
    }
}

/// <summary>Repositorio de reservas que simula que el índice único rechaza el guardado (RN-RES-01).</summary>
class ReservaRepositoryConDuplicado : IReservaRepository
{
    private readonly IReservaRepository _real;
    public ReservaRepositoryConDuplicado(IReservaRepository real) => _real = real;

    public Task GuardarCambiosAsync(CancellationToken cancellationToken) =>
        throw new ReservaDuplicadaException("Esa mesa ya fue tomada, intenta otra franja.", new InvalidOperationException());

    public Task<IReadOnlyList<Reserva>> ListarVigentesPorFechaAsync(DateOnly fecha, CancellationToken cancellationToken) =>
        _real.ListarVigentesPorFechaAsync(fecha, cancellationToken);
    public Task<int> ContarVigentesPorTelefonoAsync(string telefono, CancellationToken cancellationToken) =>
        _real.ContarVigentesPorTelefonoAsync(telefono, cancellationToken);
    public Task<Reserva?> BuscarPorCodigoYTelefonoAsync(string codigo, string telefono, CancellationToken cancellationToken) =>
        _real.BuscarPorCodigoYTelefonoAsync(codigo, telefono, cancellationToken);
    public Task<PagedResult<ReservaListadoDto>> ListarAsync(
        DateOnly? fecha, EstadoReserva? estado, Guid? franjaId, string? telefono, int page, int pageSize, CancellationToken cancellationToken) =>
        _real.ListarAsync(fecha, estado, franjaId, telefono, page, pageSize, cancellationToken);
    public Task<Reserva?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        _real.BuscarPorIdAsync(id, cancellationToken);
    public Task<ReservaDetalleDto?> ObtenerDetalleAsync(Guid id, CancellationToken cancellationToken) =>
        _real.ObtenerDetalleAsync(id, cancellationToken);
    public Task<ReservaDetalleDto?> ObtenerDetallePorCodigoAsync(string codigo, CancellationToken cancellationToken) =>
        _real.ObtenerDetallePorCodigoAsync(codigo, cancellationToken);
    public Task<IReadOnlyList<ReservaConFranja>> ListarPorEstadoHastaAsync(EstadoReserva estado, DateOnly fechaMaxima, int limite, CancellationToken cancellationToken) =>
        _real.ListarPorEstadoHastaAsync(estado, fechaMaxima, limite, cancellationToken);
    public void Descartar(Reserva reserva) => _real.Descartar(reserva);
    public Task<bool> ExisteVigenteAsync(Guid mesaId, DateOnly fecha, Guid franjaId, CancellationToken cancellationToken) =>
        _real.ExisteVigenteAsync(mesaId, fecha, franjaId, cancellationToken);
    public Task<bool> ExisteCodigoAsync(string codigo, CancellationToken cancellationToken) =>
        _real.ExisteCodigoAsync(codigo, cancellationToken);
    public void Agregar(Reserva reserva) => _real.Agregar(reserva);
}
