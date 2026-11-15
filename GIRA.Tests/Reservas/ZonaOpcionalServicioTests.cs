using GIRA.Application.Common;
using GIRA.Application.Common.Reservas;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Services.Reservas;
using GIRA.Application.Validations.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Infrastructure.Services.Reservas;
using GIRA.Tests.Common;
using Microsoft.Extensions.Logging.Abstractions;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Zona opcional en la reserva web (GI-4xx): disponibilidad por zona, asignación limitada a la zona elegida,
/// ZONA_SIN_MESAS sin caer en silencio a otra zona, y el canal del personal que la ignora. Repositorios falsos.
/// </summary>
public class ZonaOpcionalServicioTests
{
    private static readonly DateOnly Fecha = new(2026, 12, 25);
    private static readonly FranjaHoraria Almuerzo = new()
    {
        Id = Guid.NewGuid(), Nombre = "Almuerzo", HoraInicio = new TimeOnly(12, 0), HoraFin = new TimeOnly(15, 0)
    };

    private static readonly Zona Salon = new() { Id = Guid.NewGuid(), Nombre = "Salón", Activa = true };
    private static readonly Zona Terraza = new() { Id = Guid.NewGuid(), Nombre = "Terraza", Activa = true };
    private static readonly Zona Cerrada = new() { Id = Guid.NewGuid(), Nombre = "Cerrada", Activa = false };

    private static Mesa MesaEn(Zona zona, int numero, int capacidad = 4) =>
        new() { Id = Guid.NewGuid(), Numero = numero, Capacidad = capacidad, ZonaId = zona.Id, Activa = true };

    private static Reserva ReservaEn(Mesa mesa) => new()
    {
        Id = Guid.NewGuid(), Codigo = "AAAA1111", MesaId = mesa.Id, FranjaHorariaId = Almuerzo.Id, Fecha = Fecha,
        Personas = 2
    };

    private static DateTimeOffset AhoraPorDefecto =>
        new(Fecha.AddDays(-1).ToDateTime(new TimeOnly(8, 0)).AddHours(4), TimeSpan.Zero);

    private static ReservaService CrearReservas(
        IReadOnlyList<Mesa> mesas, ReservaRepositoryFake? reservaRepo = null, VisitaRepositoryFalso? visitas = null,
        MesaGestionRepositoryFalso? mesaGestion = null, ClienteRepositoryFake? clienteRepo = null,
        params Zona[] zonas)
    {
        reservaRepo ??= new ReservaRepositoryFake();
        var parametros = new ParametrosReservaFalso();
        var reloj = new TimeProviderFalso(AhoraPorDefecto);
        var publisher = new PublisherFalso();

        return new ReservaService(
            new MesaRepositoryFalso(mesas),
            new FranjaHorariaRepositoryFalso([Almuerzo]),
            reservaRepo,
            clienteRepo ?? new ClienteRepositoryFake(),
            mesaGestion ?? new MesaGestionRepositoryFalso(),
            visitas ?? new VisitaRepositoryFalso(),
            new ZonaRepositoryFalso(zonas),
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
    }

    private static DisponibilidadService CrearDisponibilidad(
        IReadOnlyList<Mesa> mesas, IReadOnlyList<Reserva>? reservas = null, IReadOnlyList<VisitaSinReserva>? visitas = null)
    {
        var parametros = new ParametrosReservaFalso();
        var reloj = new TimeProviderFalso(AhoraPorDefecto);
        return new DisponibilidadService(
            new MesaRepositoryFalso(mesas),
            new FranjaHorariaRepositoryFalso([Almuerzo]),
            new ReservaRepositoryFalso(reservas ?? []),
            new VisitaRepositoryFalso([.. visitas ?? []]),
            new ZonaRepositoryFalso(Salon, Terraza),
            parametros,
            new ConsultarDisponibilidadRequestValidator(parametros, reloj),
            reloj);
    }

    private static CrearReservaRequest Web(Guid? zonaId, int personas = 2) =>
        new("Ana Pérez", "8095551234", Fecha, Almuerzo.Id, personas, ZonaId: zonaId);

    // --- Disponibilidad por zona ---

    [Fact]
    public async Task Disponibilidad_ZonaSinMesaConCapacidadSuficiente_NoApareceEnLaLista()
    {
        // Salón tiene una mesa de 2 y Terraza una de 6: para 4 personas solo Terraza sirve.
        var servicio = CrearDisponibilidad([MesaEn(Salon, 1, capacidad: 2), MesaEn(Terraza, 2, capacidad: 6)]);

        var resultado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 4), default);

        var franja = Assert.Single(resultado.Value!);
        var zona = Assert.Single(franja.ZonasDisponibles);
        Assert.Equal(Terraza.Id, zona.Id);
    }

    [Fact]
    public async Task Disponibilidad_MesaOcupadaPorReserva_SuZonaYaNoApareceSiNoLeQuedaOtra()
    {
        var mesaSalon = MesaEn(Salon, 1);
        var mesaTerraza = MesaEn(Terraza, 2);
        var servicio = CrearDisponibilidad([mesaSalon, mesaTerraza], reservas: [ReservaEn(mesaSalon)]);

        var resultado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);

        var zonas = Assert.Single(resultado.Value!).ZonasDisponibles;
        Assert.Equal([Terraza.Id], zonas.Select(z => z.Id));
    }

    [Fact]
    public async Task Disponibilidad_MesaBloqueadaPorVisita_NoCuentaParaSuZona()
    {
        var mesaSalon = MesaEn(Salon, 1);
        var mesaTerraza = MesaEn(Terraza, 2);
        var visita = new VisitaSinReserva
        {
            Id = Guid.NewGuid(), Fecha = Fecha, FranjaHorariaId = Almuerzo.Id, MesaId = mesaTerraza.Id, PersonasReales = 2
        };
        var servicio = CrearDisponibilidad([mesaSalon, mesaTerraza], visitas: [visita]);

        var resultado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);

        var zonas = Assert.Single(resultado.Value!).ZonasDisponibles;
        Assert.Equal([Salon.Id], zonas.Select(z => z.Id));
    }

    [Fact]
    public async Task Disponibilidad_DosZonasLibres_LasDevuelveOrdenadasPorNombre()
    {
        var servicio = CrearDisponibilidad([MesaEn(Terraza, 1), MesaEn(Salon, 2)]);

        var resultado = await servicio.ConsultarAsync(new ConsultarDisponibilidadRequest(Fecha, 2), default);

        Assert.Equal(["Salón", "Terraza"], Assert.Single(resultado.Value!).ZonasDisponibles.Select(z => z.Nombre));
    }

    // --- Creación con y sin zona ---

    [Fact]
    public async Task CrearAsync_ConZona_AsignaUnaMesaDeEsaZonaAunqueOtraSeaMasChica()
    {
        // La mesa más chica (2) está en Salón; se pide Terraza: debe ir a la de Terraza (4).
        var servicio = CrearReservas([MesaEn(Salon, 1, capacidad: 2), MesaEn(Terraza, 2, capacidad: 4)], zonas: [Salon, Terraza]);

        var resultado = await servicio.CrearAsync(Web(Terraza.Id), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("Terraza", resultado.Value!.ZonaNombre);
    }

    [Fact]
    public async Task CrearAsync_SinZona_ElegiaLaMesaMasChicaDeCualquierZonaComoAntes()
    {
        var servicio = CrearReservas([MesaEn(Terraza, 2, capacidad: 4), MesaEn(Salon, 1, capacidad: 2)], zonas: [Salon, Terraza]);

        var resultado = await servicio.CrearAsync(Web(null), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("Salón", resultado.Value!.ZonaNombre);
    }

    [Fact]
    public async Task CrearAsync_ZonaInactiva_EsRechazada()
    {
        var servicio = CrearReservas([MesaEn(Salon, 1)], zonas: [Salon, Cerrada]);

        var resultado = await servicio.CrearAsync(Web(Cerrada.Id), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Validation, resultado.Type);
    }

    [Fact]
    public async Task CrearAsync_ZonaInexistente_EsRechazada()
    {
        var servicio = CrearReservas([MesaEn(Salon, 1)], zonas: [Salon]);

        var resultado = await servicio.CrearAsync(Web(Guid.NewGuid()), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Validation, resultado.Type);
    }

    // --- Zona llena ---

    [Fact]
    public async Task CrearAsync_ZonaLlena_Devuelve409ZonaSinMesasConElNombreYNoCaeEnOtraZona()
    {
        var mesaSalon = MesaEn(Salon, 1);
        var mesaTerraza = MesaEn(Terraza, 2);
        var reservaRepo = new ReservaRepositoryFake { VigentesPorFecha = [ReservaEn(mesaSalon)] };
        var servicio = CrearReservas([mesaSalon, mesaTerraza], reservaRepo, zonas: [Salon, Terraza]);

        var resultado = await servicio.CrearAsync(Web(Salon.Id), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Conflict, resultado.Type);
        Assert.Equal(CodigosErrorReservas.ZonaSinMesas, resultado.Codigo);
        Assert.Equal("La zona Salón ya no tiene mesas disponibles en esa franja. Elige otra zona o \"sin preferencia\".", resultado.Error);
        Assert.Empty(reservaRepo.Agregadas); // Terraza estaba libre y NO se usó en silencio.
    }

    [Fact]
    public async Task CrearAsync_ZonaConMesaBloqueadaPorVisita_Devuelve409ZonaSinMesas()
    {
        var mesaSalon = MesaEn(Salon, 1);
        var visita = new VisitaSinReserva
        {
            Id = Guid.NewGuid(), Fecha = Fecha, FranjaHorariaId = Almuerzo.Id, MesaId = mesaSalon.Id, PersonasReales = 2
        };
        var servicio = CrearReservas([mesaSalon, MesaEn(Terraza, 2)], visitas: new VisitaRepositoryFalso(visita), zonas: [Salon, Terraza]);

        var resultado = await servicio.CrearAsync(Web(Salon.Id), default);

        Assert.Equal(CodigosErrorReservas.ZonaSinMesas, resultado.Codigo);
    }

    // --- Canal del personal (C3: ya no se ignora en silencio, se rechaza con 400) ---

    [Fact]
    public async Task CrearComoPersonal_ConZonaId_EsRechazadaCon400YElCodigoEstable()
    {
        var servicio = CrearReservas([MesaEn(Salon, 1)], zonas: [Salon]);
        var request = new CrearReservaRequest(
            "Ana Pérez", "8095551234", Fecha, Almuerzo.Id, 2, ZonaId: Guid.NewGuid());

        var resultado = await servicio.CrearComoPersonalAsync(request, OrigenReserva.Telefono, default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Validation, resultado.Type);
        Assert.Equal(CodigosErrorReservas.ZonaNoAplicaPersonal, resultado.Codigo);
        Assert.Equal("El canal del personal no usa zona; la mesa se asigna manualmente.", resultado.Error);
    }

    [Fact]
    public async Task CrearComoPersonal_SinZonaId_NoCambia()
    {
        var servicio = CrearReservas([MesaEn(Salon, 1)], zonas: [Salon]);
        var request = new CrearReservaRequest("Ana Pérez", "8095551234", Fecha, Almuerzo.Id, 2, ZonaId: null);

        var resultado = await servicio.CrearComoPersonalAsync(request, OrigenReserva.Telefono, default);

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public async Task CrearWeb_ConZonaId_NoCambia()
    {
        var servicio = CrearReservas([MesaEn(Salon, 1)], zonas: [Salon]);

        var resultado = await servicio.CrearAsync(Web(Salon.Id), default);

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public async Task CrearWeb_SinZonaId_NoCambia()
    {
        var servicio = CrearReservas([MesaEn(Salon, 1)], zonas: [Salon]);

        var resultado = await servicio.CrearAsync(Web(null), default);

        Assert.True(resultado.IsSuccess);
    }

    // --- Consulta pública ---

    [Fact]
    public void Dtos_PublicosNoTienenNumeroNiIdDeMesaNiCapacidad()
    {
        foreach (var tipo in new[] { typeof(ReservaCreadaDto), typeof(ReservaConsultadaDto), typeof(ZonaDisponibleDto) })
        {
            var nombres = tipo.GetProperties().Select(p => p.Name).ToList();
            Assert.DoesNotContain(nombres, n => n.Contains("Mesa", StringComparison.OrdinalIgnoreCase) && n != "ZonaNombre");
            Assert.DoesNotContain("Capacidad", nombres);
        }
    }

    [Fact]
    public async Task ConsultarPorCodigo_MuestraLaZonaActualDeLaMesaYNoElNumero()
    {
        var mesaTerraza = MesaEn(Terraza, 12);
        var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Ana Pérez", Telefono = "8095551234" };
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(), Codigo = "12345678", ClienteId = cliente.Id, MesaId = mesaTerraza.Id,
            FranjaHorariaId = Almuerzo.Id, Fecha = Fecha, Personas = 2
        };
        var reservaRepo = new ReservaRepositoryFake
        {
            ReservaPorCodigoYTelefono = reserva, ClienteDeLaReservaPorCodigoYTelefono = cliente
        };
        var servicio = CrearReservas(
            [mesaTerraza], reservaRepo, mesaGestion: new MesaGestionRepositoryFalso { MesaFija = mesaTerraza },
            clienteRepo: new ClienteRepositoryFake { ClientePorId = cliente },
            zonas: [Salon, Terraza]);

        var resultado = await servicio.ConsultarPorCodigoAsync(new ConsultarReservaRequest("12345678", "8095551234"), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("Terraza", resultado.Value!.ZonaNombre);
    }
}
