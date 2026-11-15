using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Services.Reservas;
using GIRA.Application.Validations.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Repositories.Reservas;
using GIRA.Infrastructure.Services.Reservas;
using GIRA.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Una visita sin reserva ocupa su mesa en su fecha y franja para las reservas del personal: la asignación
/// automática la salta y la mesa elegida a mano se rechaza con su propio motivo. No se mira la hora actual
/// (la visita no tiene hora de salida), así que la mesa queda tomada durante toda esa franja. El mapa de
/// ocupación sigue sin contarla (GI-402), que es una vista distinta.
/// </summary>
public class VisitaYMesaDeReservaTests : IDisposable
{
    private static readonly DateOnly Dia = new(2026, 12, 1);

    private readonly GiraDbContext _db;
    private readonly ParametrosReservaFalso _parametros = new();
    private readonly PublisherFalso _publisher = new();
    private readonly ReservaService _servicio;
    private readonly Zona _zona = new() { Id = Guid.NewGuid(), Nombre = "Salón" };

    private readonly Mesa _mesa1;
    private readonly Mesa _mesa2;
    private readonly FranjaHoraria _almuerzo = new()
    {
        Id = Guid.NewGuid(), Nombre = "Almuerzo", HoraInicio = new TimeOnly(12, 0), HoraFin = new TimeOnly(13, 45)
    };
    private readonly FranjaHoraria _tarde = new()
    {
        Id = Guid.NewGuid(), Nombre = "Tarde", HoraInicio = new TimeOnly(14, 0), HoraFin = new TimeOnly(15, 45)
    };

    public VisitaYMesaDeReservaTests()
    {
        var opciones = new DbContextOptionsBuilder<GiraDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options;
        _db = new GiraDbContext(opciones);
        _mesa1 = new Mesa { Id = Guid.NewGuid(), Numero = 1, Capacidad = 4, ZonaId = _zona.Id };
        _mesa2 = new Mesa { Id = Guid.NewGuid(), Numero = 2, Capacidad = 4, ZonaId = _zona.Id };

        var reloj = new TimeProviderFalso(new DateTimeOffset(2026, 12, 1, 16, 0, 0, TimeSpan.Zero)); // 12:00 local
        _servicio = new ReservaService(
            new MesaRepository(_db), new FranjaHorariaRepository(_db), new ReservaRepository(_db), new ClienteRepository(_db),
            new MesaGestionRepository(_db), new VisitaRepository(_db), new ZonaRepository(_db), _parametros,
            new CrearReservaRequestValidator(_parametros, reloj), new ListarReservasRequestValidator(),
            new CheckInRequestValidator(), new CancelarPorPersonalRequestValidator(),
            new ModificarReservaRequestValidator(), new GeneradorCodigoReserva(), _publisher,
            new PublicadorEventosSeguro(_publisher, NullLogger<PublicadorEventosSeguro>.Instance), reloj);
    }

    public void Dispose() => _db.Dispose();

    private async Task SembrarSalonAsync(bool conSegundaMesa = false)
    {
        _db.Zonas.Add(_zona);
        _db.Mesas.Add(_mesa1);
        if (conSegundaMesa)
        {
            _db.Mesas.Add(_mesa2);
        }

        _db.FranjasHorarias.AddRange(_almuerzo, _tarde);
        await _db.SaveChangesAsync();
    }

    private async Task SembrarVisitaAsync(Guid franjaId, Guid mesaId, DateOnly? fecha = null)
    {
        _db.VisitasSinReserva.Add(new VisitaSinReserva
        {
            Id = Guid.NewGuid(), Fecha = fecha ?? Dia, FranjaHorariaId = franjaId, MesaId = mesaId, PersonasReales = 2
        });
        await _db.SaveChangesAsync();
    }

    private async Task<Reserva> SembrarReservaAsync(Guid mesaId, Guid franjaId, DateOnly? fecha = null)
    {
        var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Cliente Ejemplo", Telefono = "8095550101" };
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(), Codigo = "20000001", ClienteId = cliente.Id, MesaId = mesaId,
            FranjaHorariaId = franjaId, Fecha = fecha ?? Dia, Personas = 2, Origen = OrigenReserva.Telefono
        };
        _db.Clientes.Add(cliente);
        _db.Reservas.Add(reserva);
        await _db.SaveChangesAsync();
        return reserva;
    }

    private Task<OperationResult<ReservaCreadaDto>> CrearAsync(Guid franjaId, int personas = 2) =>
        _servicio.CrearComoPersonalAsync(
            new CrearReservaRequest("Ana Pérez", "8095551234", Dia, franjaId, personas), OrigenReserva.Telefono, default);

    // --- Asignación automática ---

    [Fact]
    public async Task La_Asignacion_Automatica_Salta_La_Mesa_Con_Visita_Y_Toma_La_Otra()
    {
        await SembrarSalonAsync(conSegundaMesa: true);
        await SembrarVisitaAsync(_almuerzo.Id, _mesa1.Id);

        var resultado = await CrearAsync(_almuerzo.Id);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(_mesa2.Id, (await _db.Reservas.SingleAsync()).MesaId);
    }

    [Fact]
    public async Task Si_La_Visita_Ocupa_La_Unica_Mesa_No_Queda_Ninguna_Para_La_Reserva()
    {
        await SembrarSalonAsync();
        await SembrarVisitaAsync(_almuerzo.Id, _mesa1.Id);

        var resultado = await CrearAsync(_almuerzo.Id);

        Assert.Equal(ErrorType.Conflict, resultado.Type);
        Assert.Equal("Esa mesa ya fue tomada, intenta otra franja.", resultado.Error);
        Assert.Empty(await _db.Reservas.ToListAsync());
    }

    [Theory]
    [InlineData(true)]  // visita en otra franja del mismo día
    [InlineData(false)] // visita en la misma franja, pero de otra fecha
    public async Task Una_Visita_De_Otra_Franja_O_De_Otra_Fecha_No_Bloquea_La_Mesa(bool otraFranja)
    {
        await SembrarSalonAsync();
        if (otraFranja)
        {
            await SembrarVisitaAsync(_tarde.Id, _mesa1.Id);
        }
        else
        {
            await SembrarVisitaAsync(_almuerzo.Id, _mesa1.Id, Dia.AddDays(-1));
        }

        var resultado = await CrearAsync(_almuerzo.Id);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(_mesa1.Id, (await _db.Reservas.SingleAsync()).MesaId);
    }

    [Fact]
    public async Task Una_Visita_Sin_Mesa_No_Bloquea_Ninguna_Mesa()
    {
        await SembrarSalonAsync();
        _db.VisitasSinReserva.Add(new VisitaSinReserva
        {
            Id = Guid.NewGuid(), Fecha = Dia, FranjaHorariaId = _almuerzo.Id, MesaId = null, PersonasReales = 2
        });
        await _db.SaveChangesAsync();

        Assert.True((await CrearAsync(_almuerzo.Id)).IsSuccess);
    }

    // --- Mesa elegida a mano (modificar) ---

    [Fact]
    public async Task Modificar_Hacia_Una_Mesa_Con_Visita_Es_Conflict_Con_El_Motivo_De_La_Visita()
    {
        await SembrarSalonAsync(conSegundaMesa: true);
        await SembrarVisitaAsync(_tarde.Id, _mesa2.Id);
        var reserva = await SembrarReservaAsync(_mesa1.Id, _tarde.Id);

        var resultado = await _servicio.ModificarAsync(
            reserva.Id, new ModificarReservaRequest(Dia, _tarde.Id, 2, _mesa2.Id), default);

        Assert.Equal(ErrorType.Conflict, resultado.Type);
        Assert.Equal(
            "La mesa 2 tiene una visita sin reserva en esta franja. Elige otra mesa o franja.", resultado.Error);
        Assert.Equal(_mesa1.Id, (await _db.Reservas.SingleAsync(r => r.Id == reserva.Id)).MesaId);
    }

    [Fact]
    public async Task Modificar_Hacia_Una_Mesa_Con_Reserva_Conserva_El_Mensaje_De_Reserva()
    {
        await SembrarSalonAsync(conSegundaMesa: true);
        var ocupante = await SembrarReservaAsync(_mesa2.Id, _tarde.Id);
        var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Otro", Telefono = "8095550102" };
        var propia = new Reserva
        {
            Id = Guid.NewGuid(), Codigo = "20000002", ClienteId = cliente.Id, MesaId = _mesa1.Id,
            FranjaHorariaId = _tarde.Id, Fecha = Dia, Personas = 2, Origen = OrigenReserva.Telefono
        };
        _db.Clientes.Add(cliente);
        _db.Reservas.Add(propia);
        await _db.SaveChangesAsync();

        var resultado = await _servicio.ModificarAsync(
            propia.Id, new ModificarReservaRequest(Dia, _tarde.Id, 2, _mesa2.Id), default);

        Assert.Equal(ErrorType.Conflict, resultado.Type);
        Assert.Equal("La mesa 2 ya está reservada en esa fecha y franja.", resultado.Error);
        Assert.NotNull(ocupante);
    }

    [Fact]
    public async Task Modificar_Con_La_Mesa_Actual_Tomada_Por_Una_Visita_Reasigna_A_Otra_Mesa()
    {
        // La mesa actual (1) queda con una visita: al modificar las personas, el servicio mueve la reserva
        // a la mesa 2 en vez de fallar (la mesa actual es solo "preferida", no obligatoria).
        await SembrarSalonAsync(conSegundaMesa: true);
        var reserva = await SembrarReservaAsync(_mesa1.Id, _tarde.Id);
        await SembrarVisitaAsync(_tarde.Id, _mesa1.Id);

        var resultado = await _servicio.ModificarAsync(
            reserva.Id, new ModificarReservaRequest(Dia, _tarde.Id, 3, null), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(_mesa2.Id, (await _db.Reservas.SingleAsync(r => r.Id == reserva.Id)).MesaId);
    }
}
