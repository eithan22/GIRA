using Microsoft.Extensions.Logging.Abstractions;
using GIRA.Infrastructure.Services.Reservas;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Application.Services.Reservas;
using GIRA.Application.Validations.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Domain.Exceptions.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Repositories.Reservas;
using GIRA.Tests.Common;
using Microsoft.EntityFrameworkCore;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Pruebas del check-in (GI-165, GI-232): ventana de 15 minutos, límite en el fin de la franja,
/// capacidad de la mesa, estados y mensajes. Repositorios reales sobre EF Core InMemory y reloj
/// simulado (hora local de Santo Domingo = UTC-4).
/// </summary>
public class CheckInServicioTests
{
    // Almuerzo 12:00 a 15:00 del 1 de diciembre de 2026 (hora local).
    private static readonly DateOnly Dia = new(2026, 12, 1);

    private static DateTimeOffset Utc(DateOnly dia, int hora, int minuto) =>
        new(dia.ToDateTime(new TimeOnly(hora, minuto)).AddHours(4), TimeSpan.Zero);

    private sealed class Entorno : IDisposable
    {
        public GiraDbContext Contexto { get; }
        public ReservaService Servicio { get; }
        public PublisherFalso Publisher { get; } = new();
        public Mesa Mesa { get; }
        public Reserva Reserva { get; private set; } = null!;

        public Entorno(DateTimeOffset ahoraUtc, bool conflictoAlGuardar = false)
        {
            var opciones = new DbContextOptionsBuilder<GiraDbContext>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options;
            Contexto = new GiraDbContext(opciones);

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
                Publisher,
                new PublicadorEventosSeguro(Publisher, NullLogger<PublicadorEventosSeguro>.Instance),
                reloj);

            Mesa = new Mesa { Id = Guid.NewGuid(), Numero = 2, Capacidad = 4 };
        }

        public async Task SembrarAsync(
            EstadoReserva estado = EstadoReserva.Confirmada, int personas = 2, DateOnly? fecha = null)
        {
            var zona = new Zona { Id = Guid.NewGuid(), Nombre = "Salón" };
            Mesa.ZonaId = zona.Id;
            var franja = new FranjaHoraria
            {
                Id = Guid.NewGuid(), Nombre = "Almuerzo", HoraInicio = new TimeOnly(12, 0), HoraFin = new TimeOnly(15, 0)
            };
            var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Cliente Ejemplo", Telefono = "8095550101" };
            Reserva = new Reserva
            {
                Id = Guid.NewGuid(), Codigo = "10000001", ClienteId = cliente.Id, MesaId = Mesa.Id,
                FranjaHorariaId = franja.Id, Fecha = fecha ?? Dia, Personas = personas, Origen = OrigenReserva.Telefono
            };
            switch (estado)
            {
                case EstadoReserva.EnCurso:
                    Reserva.HacerCheckIn(personas, DateTime.UtcNow);
                    break;
                case EstadoReserva.Completada:
                    Reserva.HacerCheckIn(personas, DateTime.UtcNow);
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
            Contexto.Mesas.Add(Mesa);
            Contexto.FranjasHorarias.Add(franja);
            Contexto.Clientes.Add(cliente);
            Contexto.Reservas.Add(Reserva);
            await Contexto.SaveChangesAsync();
        }

        public Task<OperationResult<ReservaDetalleDto>> CheckInAsync(int personasReales) =>
            Servicio.CheckInAsync(Reserva.Id, new CheckInRequest(personasReales), default);

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
    public async Task CheckIn_Dentro_De_La_Ventana_Pasa_A_EnCurso_Y_Guarda_Personas_Y_Hora()
    {
        var ahora = Utc(Dia, 11, 45);
        using var e = new Entorno(ahora);
        await e.SembrarAsync();

        var resultado = await e.CheckInAsync(3);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("EnCurso", resultado.Value!.Estado);
        Assert.Equal(3, resultado.Value.PersonasReales);
        var guardada = await e.Contexto.Reservas.SingleAsync();
        Assert.Equal(EstadoReserva.EnCurso, guardada.Estado);
        Assert.Equal(3, guardada.PersonasReales);
        Assert.Equal(ahora.UtcDateTime, guardada.FechaCheckIn);
    }

    [Fact]
    public async Task CheckIn_No_Publica_Ningun_Evento()
    {
        using var e = new Entorno(Utc(Dia, 12, 0));
        await e.SembrarAsync();

        var resultado = await e.CheckInAsync(2);

        Assert.True(resultado.IsSuccess);
        Assert.Empty(e.Publisher.Publicados);
    }

    // --- Ventana de 15 minutos (valor por defecto; configurable, GI-226) ---

    [Fact]
    public async Task CheckIn_Justo_15_Minutos_Antes_Pasa()
    {
        using var e = new Entorno(Utc(Dia, 11, 45));
        await e.SembrarAsync();

        Assert.True((await e.CheckInAsync(2)).IsSuccess);
    }

    [Fact]
    public async Task CheckIn_16_Minutos_Antes_Es_Conflict_Solo_Con_La_Hora_Si_La_Reserva_Es_De_Hoy()
    {
        using var e = new Entorno(Utc(Dia, 11, 44));
        await e.SembrarAsync();

        var resultado = await e.CheckInAsync(2);

        EsFallo(resultado, ErrorType.Conflict, "El check-in se habilita a partir de las 11:45.");
        Assert.Equal(EstadoReserva.Confirmada, (await e.Contexto.Reservas.SingleAsync()).Estado);
    }

    [Fact]
    public async Task CheckIn_De_Una_Reserva_De_Otro_Dia_Incluye_La_Fecha_En_El_Mensaje()
    {
        using var e = new Entorno(Utc(new DateOnly(2026, 11, 30), 10, 0));
        await e.SembrarAsync();

        var resultado = await e.CheckInAsync(2);

        EsFallo(resultado, ErrorType.Conflict, "El check-in se habilita el 01/12 a partir de las 11:45.");
    }

    // --- Límite superior: fin de la franja ---

    [Fact]
    public async Task CheckIn_Un_Minuto_Antes_Del_Fin_De_La_Franja_Pasa()
    {
        using var e = new Entorno(Utc(Dia, 14, 59));
        await e.SembrarAsync();

        Assert.True((await e.CheckInAsync(2)).IsSuccess);
    }

    [Fact]
    public async Task CheckIn_Exactamente_En_El_Fin_De_La_Franja_Es_Conflict()
    {
        using var e = new Entorno(Utc(Dia, 15, 0));
        await e.SembrarAsync();

        var resultado = await e.CheckInAsync(2);

        EsFallo(resultado, ErrorType.Conflict, "La franja de esta reserva ya terminó; no se puede hacer check-in.");
    }

    // --- Capacidad (RN-RES-15) ---

    [Fact]
    public async Task CheckIn_Con_Mas_Personas_Que_Las_Reservadas_Pero_Dentro_De_La_Capacidad_Pasa()
    {
        using var e = new Entorno(Utc(Dia, 12, 0));
        await e.SembrarAsync(personas: 2); // mesa de 4

        var resultado = await e.CheckInAsync(4);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(4, resultado.Value!.PersonasReales);
        Assert.Equal(2, resultado.Value.Personas);
    }

    [Fact]
    public async Task CheckIn_Con_Mas_Personas_Que_La_Capacidad_Es_Validation_Y_No_Cambia_La_Reserva()
    {
        using var e = new Entorno(Utc(Dia, 12, 0));
        await e.SembrarAsync();

        var resultado = await e.CheckInAsync(5);

        EsFallo(resultado, ErrorType.Validation, "La mesa 2 admite como máximo 4 personas.");
        var guardada = await e.Contexto.Reservas.SingleAsync();
        Assert.Equal(EstadoReserva.Confirmada, guardada.Estado);
        Assert.Null(guardada.PersonasReales);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task CheckIn_Con_Menos_De_1_Persona_Es_Validation(int personas)
    {
        using var e = new Entorno(Utc(Dia, 12, 0));
        await e.SembrarAsync();

        var resultado = await e.CheckInAsync(personas);

        EsFallo(resultado, ErrorType.Validation, "Debe haber al menos 1 persona.");
    }

    // --- Estados ---

    [Theory]
    [InlineData(EstadoReserva.EnCurso, "Esta reserva ya tiene el check-in registrado.")]
    [InlineData(EstadoReserva.Cancelada, "La reserva fue cancelada; no se puede hacer check-in.")]
    [InlineData(EstadoReserva.NoAsistio, "La reserva fue marcada como no asistida. Si el cliente llegó, regístralo como visita sin reserva.")]
    [InlineData(EstadoReserva.Completada, "La reserva ya fue completada.")]
    public async Task CheckIn_Fuera_De_Estado_Confirmada_Es_Conflict_Con_El_Mensaje_Del_Estado(
        EstadoReserva estado, string mensaje)
    {
        using var e = new Entorno(Utc(Dia, 12, 0));
        await e.SembrarAsync(estado);

        var resultado = await e.CheckInAsync(2);

        EsFallo(resultado, ErrorType.Conflict, mensaje);
    }

    [Fact]
    public async Task Un_Segundo_CheckIn_Sobre_La_Misma_Reserva_Es_Conflict_Y_No_Cambia_Los_Datos()
    {
        using var e = new Entorno(Utc(Dia, 12, 0));
        await e.SembrarAsync();
        Assert.True((await e.CheckInAsync(2)).IsSuccess);

        var segundo = await e.CheckInAsync(4);

        EsFallo(segundo, ErrorType.Conflict, "Esta reserva ya tiene el check-in registrado.");
        Assert.Equal(2, (await e.Contexto.Reservas.SingleAsync()).PersonasReales);
    }

    // --- Otros errores ---

    [Fact]
    public async Task CheckIn_De_Una_Reserva_Inexistente_Es_NotFound()
    {
        using var e = new Entorno(Utc(Dia, 12, 0));

        var resultado = await e.Servicio.CheckInAsync(Guid.NewGuid(), new CheckInRequest(2), default);

        EsFallo(resultado, ErrorType.NotFound, "No encontramos esa reserva.");
    }

    [Fact]
    public async Task CheckIn_Con_Conflicto_De_Concurrencia_Es_Conflict_Con_Mensaje_Propio()
    {
        using var e = new Entorno(Utc(Dia, 12, 0), conflictoAlGuardar: true);
        await e.SembrarAsync();

        var resultado = await e.CheckInAsync(2);

        EsFallo(resultado, ErrorType.Conflict, "La reserva cambió mientras se procesaba el check-in; vuelve a consultarla.");
    }

    [Fact]
    public async Task CheckIn_Con_La_Mesa_Borrada_Es_Una_Violacion_De_Integridad()
    {
        using var e = new Entorno(Utc(Dia, 12, 0));
        await e.SembrarAsync();
        e.Mesa.IsDeleted = true;
        await e.Contexto.SaveChangesAsync();

        await Assert.ThrowsAsync<InvalidOperationException>(() => e.CheckInAsync(2));
    }
}

/// <summary>Repositorio de reservas que simula una modificación concurrente al guardar.</summary>
class ReservaRepositoryConConflicto : IReservaRepository
{
    private readonly IReservaRepository _real;
    public ReservaRepositoryConConflicto(IReservaRepository real) => _real = real;

    public Task GuardarCambiosAsync(CancellationToken cancellationToken) =>
        throw new ReservaConflictoConcurrenciaException("conflicto simulado", new InvalidOperationException());

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

/// <summary>Repositorio de gestión de mesas que no se usa en las pruebas de creación de reservas.</summary>
class MesaGestionRepositoryFalso : IMesaGestionRepository
{
    public Task<Mesa?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<bool> ExisteNumeroAsync(int numero, Guid? excluirId, CancellationToken cancellationToken) => throw new NotImplementedException();
    public Task<PagedResult<Mesa>> ListarAsync(Guid? zonaId, bool? activa, int page, int pageSize, CancellationToken cancellationToken) => throw new NotImplementedException();
    public void Agregar(Mesa mesa) => throw new NotImplementedException();
    public Task GuardarCambiosAsync(CancellationToken cancellationToken) => throw new NotImplementedException();
}
