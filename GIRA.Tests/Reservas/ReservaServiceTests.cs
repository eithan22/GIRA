using Microsoft.Extensions.Logging.Abstractions;
using GIRA.Infrastructure.Services.Reservas;
using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
using GIRA.Application.Events.Reservas;
using GIRA.Application.Interfaces.Repository.Reservas;
using GIRA.Application.Services.Reservas;
using GIRA.Application.Validations.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Domain.Exceptions.Reservas;
using GIRA.Tests.Common;
using MediatR;

namespace GIRA.Tests.Reservas;

/// <summary>Pruebas unitarias de <see cref="ReservaService"/> (GI-179), con repositorios falsos.</summary>
public class ReservaServiceTests
{
    private static readonly DateOnly Fecha = new(2026, 12, 25);
    private static readonly FranjaHoraria Almuerzo = new()
    {
        Id = Guid.NewGuid(),
        Nombre = "Almuerzo",
        HoraInicio = new TimeOnly(12, 0),
        HoraFin = new TimeOnly(15, 0)
    };
    private static readonly Mesa MesaPequena = new() { Id = Guid.NewGuid(), Numero = 1, Capacidad = 2 };

    private static ReservaService CrearServicio(
        ReservaRepositoryFake? reservaRepo = null,
        ClienteRepositoryFake? clienteRepo = null,
        IReadOnlyList<Mesa>? mesas = null,
        PublisherFalso? publisher = null,
        DateTimeOffset? ahoraUtc = null)
    {
        mesas ??= [MesaPequena];
        reservaRepo ??= new ReservaRepositoryFake();
        clienteRepo ??= new ClienteRepositoryFake();
        publisher ??= new PublisherFalso();
        var parametros = new ParametrosReservaFalso();

        // "Ahora" por defecto: la mañana del día anterior a Fecha (antelación de sobra).
        var ahoraPorDefecto = Fecha.AddDays(-1).ToDateTime(new TimeOnly(8, 0)).AddHours(4);
        var reloj = new TimeProviderFalso(ahoraUtc ?? new DateTimeOffset(ahoraPorDefecto, TimeSpan.Zero));

        var validador = new CrearReservaRequestValidator(parametros, reloj);

        return new ReservaService(
            new MesaRepositoryFalso(mesas),
            new FranjaHorariaRepositoryFalso([Almuerzo]),
            reservaRepo,
            clienteRepo,
            new MesaGestionRepositoryFalso(),
            parametros,
            validador,
            new ListarReservasRequestValidator(),
            new CheckInRequestValidator(),
            new CancelarPorPersonalRequestValidator(),
            new ModificarReservaRequestValidator(),
            new GeneradorCodigoReserva(),
            publisher,
            new PublicadorEventosSeguro(publisher, NullLogger<PublicadorEventosSeguro>.Instance),
            reloj);
    }

    private static CrearReservaRequest Request(string telefono = "8095551234", int personas = 2) =>
        new("Ana Pérez", telefono, Fecha, Almuerzo.Id, personas);

    [Fact]
    public async Task CrearAsync_ClienteNuevo_CreaLaReservaYPublicaElEvento()
    {
        var clienteRepo = new ClienteRepositoryFake();
        var publisher = new PublisherFalso();
        var servicio = CrearServicio(clienteRepo: clienteRepo, publisher: publisher);

        var resultado = await servicio.CrearAsync(Request(), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(8, resultado.Value!.Codigo.Length);
        Assert.Equal("Ana Pérez", resultado.Value.NombreCliente);
        Assert.Equal("Almuerzo", resultado.Value.FranjaNombre);
        Assert.NotNull(clienteRepo.Agregado);
        Assert.Single(publisher.Publicados);
    }

    [Fact]
    public async Task CrearAsync_ClienteExistenteConOtroNombre_ActualizaElNombre()
    {
        var clienteExistente = new Cliente { Id = Guid.NewGuid(), Nombre = "Nombre Viejo", Telefono = "8095551234" };
        var clienteRepo = new ClienteRepositoryFake(clienteExistente);
        var servicio = CrearServicio(clienteRepo: clienteRepo);

        var resultado = await servicio.CrearAsync(Request(), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("Ana Pérez", clienteExistente.Nombre);
        Assert.Equal("Ana Pérez", resultado.Value!.NombreCliente);
    }

    [Fact]
    public async Task CrearAsync_GrupoMayorQueLaMesaMasGrande_DevuelveMensajeDeLlamarAlRestaurante()
    {
        var servicio = CrearServicio(mesas: [MesaPequena]); // capacidad 2

        var resultado = await servicio.CrearAsync(Request(personas: 5), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(GIRA.Application.Common.ErrorType.Validation, resultado.Type);
        Assert.Contains("2 personas", resultado.Error);
    }

    [Fact]
    public async Task CrearAsync_FranjaInexistente_DevuelveErrorDeValidacion()
    {
        var servicio = CrearServicio();

        var resultado = await servicio.CrearAsync(
            new CrearReservaRequest("Ana", "8095551234", Fecha, Guid.NewGuid(), 2), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(GIRA.Application.Common.ErrorType.Validation, resultado.Type);
    }

    [Fact]
    public async Task CrearAsync_DiaCerrado_DevuelveErrorDeValidacion()
    {
        var parametros = new ParametrosReservaFalso { DiasCerrados = new HashSet<DayOfWeek> { Fecha.DayOfWeek } };
        var reloj = new TimeProviderFalso(new DateTimeOffset(Fecha.AddDays(-1).ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero));
        var validador = new CrearReservaRequestValidator(parametros, reloj);
        var servicio = new ReservaService(
            new MesaRepositoryFalso([MesaPequena]),
            new FranjaHorariaRepositoryFalso([Almuerzo]),
            new ReservaRepositoryFake(),
            new ClienteRepositoryFake(),
            new MesaGestionRepositoryFalso(),
            parametros,
            validador,
            new ListarReservasRequestValidator(),
            new CheckInRequestValidator(),
            new CancelarPorPersonalRequestValidator(),
            new ModificarReservaRequestValidator(),
            new GeneradorCodigoReserva(),
            new PublisherFalso(),
            new PublicadorEventosSeguro(new PublisherFalso(), NullLogger<PublicadorEventosSeguro>.Instance),
            reloj);

        var resultado = await servicio.CrearAsync(Request(), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(GIRA.Application.Common.ErrorType.Validation, resultado.Type);
    }

    [Fact]
    public async Task CrearAsync_SinAntelacionMinima_DevuelveErrorDeValidacion()
    {
        // "Ahora" muy cerca del inicio de la franja (12:00): menos de 2h de antelación.
        var ahoraUtc = new DateTimeOffset(Fecha.Year, Fecha.Month, Fecha.Day, 15, 0, 0, TimeSpan.Zero); // 11:00 RD
        var servicio = CrearServicio(ahoraUtc: ahoraUtc);

        var resultado = await servicio.CrearAsync(Request(), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(GIRA.Application.Common.ErrorType.Validation, resultado.Type);
    }

    [Fact]
    public async Task CrearAsync_TodasLasMesasOcupadas_DevuelveConflictoDeMesa()
    {
        var reservaRepo = new ReservaRepositoryFake
        {
            VigentesPorFecha = [new Reserva { MesaId = MesaPequena.Id, FranjaHorariaId = Almuerzo.Id, Fecha = Fecha }]
        };
        var servicio = CrearServicio(reservaRepo: reservaRepo);

        var resultado = await servicio.CrearAsync(Request(), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(GIRA.Application.Common.ErrorType.Conflict, resultado.Type);
        Assert.Contains("ya fue tomada", resultado.Error);
    }

    [Fact]
    public async Task CrearAsync_DosReservasVigentesDelMismoTelefono_DevuelveConflictoPorLimite()
    {
        var reservaRepo = new ReservaRepositoryFake { VigentesPorTelefono = 2 };
        var servicio = CrearServicio(reservaRepo: reservaRepo);

        var resultado = await servicio.CrearAsync(Request(), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(GIRA.Application.Common.ErrorType.Conflict, resultado.Type);
        Assert.Contains("2 reservas vigentes", resultado.Error);
    }

    [Fact]
    public async Task CrearAsync_ChoqueDeMesaAlGuardar_DevuelveConflictoDeMesa()
    {
        var reservaRepo = new ReservaRepositoryFake { LanzarConflictoAlGuardar = true };
        var servicio = CrearServicio(reservaRepo: reservaRepo);

        var resultado = await servicio.CrearAsync(Request(), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(GIRA.Application.Common.ErrorType.Conflict, resultado.Type);
    }

    [Fact]
    public async Task CrearAsync_ChoqueDeTelefonoRecuperado_UsaElClienteExistenteYCreaLaReserva()
    {
        var clienteExistente = new Cliente { Id = Guid.NewGuid(), Nombre = "Ana Pérez", Telefono = "8095551234" };
        // Primera búsqueda: null (parece nuevo). Tras el choque, segunda búsqueda: lo encuentra.
        var clienteRepo = new ClienteRepositoryFake(null, clienteExistente) { LanzarDuplicadoAlGuardar = true };
        var servicio = CrearServicio(clienteRepo: clienteRepo);

        var resultado = await servicio.CrearAsync(Request(), default);

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public async Task CrearAsync_ChoqueDeTelefonoYLaSegundaBusquedaNoEncuentraANadie_DevuelveErrorControlado()
    {
        // Primera búsqueda: null. Tras el choque, segunda búsqueda: también null (no reintenta en bucle).
        var clienteRepo = new ClienteRepositoryFake(null, null) { LanzarDuplicadoAlGuardar = true };
        var servicio = CrearServicio(clienteRepo: clienteRepo);

        var resultado = await servicio.CrearAsync(Request(), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(GIRA.Application.Common.ErrorType.Conflict, resultado.Type);
        Assert.Contains("no se pudo registrar el cliente", resultado.Error!.ToLowerInvariant());
    }

    [Fact]
    public async Task CrearAsync_CodigoSiempreOcupado_DevuelveErrorTrasCincoIntentos()
    {
        var reservaRepo = new ReservaRepositoryFake { CodigoSiempreExiste = true };
        var servicio = CrearServicio(reservaRepo: reservaRepo);

        var resultado = await servicio.CrearAsync(Request(), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(GIRA.Application.Common.ErrorType.Conflict, resultado.Type);
        Assert.Contains("código único", resultado.Error);
    }

    [Fact]
    public async Task ConsultarPorCodigoAsync_RequestNulo_Devuelve404Neutro()
    {
        var servicio = CrearServicio();

        var resultado = await servicio.ConsultarPorCodigoAsync(null!, default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(GIRA.Application.Common.ErrorType.NotFound, resultado.Type);
        Assert.Equal("Código o teléfono incorrectos.", resultado.Error);
    }

    [Fact]
    public async Task ConsultarPorCodigoAsync_CodigoNulo_Devuelve404Neutro()
    {
        var servicio = CrearServicio();

        var resultado = await servicio.ConsultarPorCodigoAsync(new ConsultarReservaRequest(null!, "8095551234"), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(GIRA.Application.Common.ErrorType.NotFound, resultado.Type);
        Assert.Equal("Código o teléfono incorrectos.", resultado.Error);
    }

    [Fact]
    public async Task ConsultarPorCodigoAsync_TelefonoNulo_Devuelve404Neutro()
    {
        var servicio = CrearServicio();

        var resultado = await servicio.ConsultarPorCodigoAsync(new ConsultarReservaRequest("12345678", null!), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(GIRA.Application.Common.ErrorType.NotFound, resultado.Type);
        Assert.Equal("Código o teléfono incorrectos.", resultado.Error);
    }

    [Fact]
    public async Task ConsultarPorCodigoAsync_TelefonoVacio_Devuelve404Neutro()
    {
        var servicio = CrearServicio();

        var resultado = await servicio.ConsultarPorCodigoAsync(new ConsultarReservaRequest("12345678", "   "), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(GIRA.Application.Common.ErrorType.NotFound, resultado.Type);
        Assert.Equal("Código o teléfono incorrectos.", resultado.Error);
    }

    [Fact]
    public async Task ConsultarPorCodigoAsync_CodigoYTelefonoConGuionesCoinciden_NormalizaYRecortaAntesDeBuscar()
    {
        var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Ana Pérez", Telefono = "8095551234" };
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Codigo = "12345678",
            ClienteId = cliente.Id,
            FranjaHorariaId = Almuerzo.Id,
            Fecha = Fecha,
            Personas = 3
        };

        var reservaRepo = new ReservaRepositoryFake
        {
            ReservaPorCodigoYTelefono = reserva,
            ClienteDeLaReservaPorCodigoYTelefono = cliente
        };
        var clienteRepo = new ClienteRepositoryFake { ClientePorId = cliente };
        var servicio = CrearServicio(reservaRepo: reservaRepo, clienteRepo: clienteRepo);

        // Código con espacios y teléfono con guiones: si el servicio dejara de recortar o
        // normalizar, el fake recibiría los valores "sucios" y esta prueba fallaría.
        var resultado = await servicio.ConsultarPorCodigoAsync(
            new ConsultarReservaRequest(" 12345678 ", "809-555-1234"), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal("12345678", reservaRepo.UltimoCodigoRecibido);
        Assert.Equal("8095551234", reservaRepo.UltimoTelefonoRecibido);
        Assert.Equal("12345678", resultado.Value!.Codigo);
        Assert.Equal("Ana Pérez", resultado.Value.NombreCliente);
        Assert.Equal("Almuerzo", resultado.Value.FranjaNombre);
        Assert.Equal("Confirmada", resultado.Value.Estado);
    }

    [Fact]
    public async Task ConsultarPorCodigoAsync_CodigoMaloOTelefonoMalo_DevuelveElMismoMensajeYTipoDeError()
    {
        var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Ana Pérez", Telefono = "8095551234" };
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(),
            Codigo = "12345678",
            ClienteId = cliente.Id,
            FranjaHorariaId = Almuerzo.Id,
            Fecha = Fecha,
            Personas = 2
        };

        var reservaRepo = new ReservaRepositoryFake
        {
            ReservaPorCodigoYTelefono = reserva,
            ClienteDeLaReservaPorCodigoYTelefono = cliente
        };
        var servicio = CrearServicio(reservaRepo: reservaRepo);

        var porCodigoMalo = await servicio.ConsultarPorCodigoAsync(
            new ConsultarReservaRequest("00000000", "8095551234"), default);
        var porTelefonoMalo = await servicio.ConsultarPorCodigoAsync(
            new ConsultarReservaRequest("12345678", "8299999999"), default);

        Assert.Equal(GIRA.Application.Common.ErrorType.NotFound, porCodigoMalo.Type);
        Assert.Equal(porCodigoMalo.Type, porTelefonoMalo.Type);
        Assert.Equal(porCodigoMalo.Error, porTelefonoMalo.Error);
    }

    [Fact]
    public async Task CancelarPorCodigoAsync_ExactamenteEnElLimite_PermiteCancelar()
    {
        var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Ana Pérez", Telefono = "8095551234" };
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(), Codigo = "12345678", ClienteId = cliente.Id,
            FranjaHorariaId = Almuerzo.Id, Fecha = Fecha, Personas = 2
        };
        var reservaRepo = new ReservaRepositoryFake
        {
            ReservaPorCodigoYTelefono = reserva,
            ClienteDeLaReservaPorCodigoYTelefono = cliente
        };
        // Franja empieza a las 12:00 local; "ahora" son las 10:00 local: justo el límite de
        // 2h, inclusivo (no debe rechazarse).
        var ahoraUtc = new DateTimeOffset(Fecha.Year, Fecha.Month, Fecha.Day, 14, 0, 0, TimeSpan.Zero); // 10:00 RD
        var servicio = CrearServicio(reservaRepo: reservaRepo, ahoraUtc: ahoraUtc);

        var resultado = await servicio.CancelarPorCodigoAsync("12345678", new CancelarReservaRequest("8095551234"), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(EstadoReserva.Cancelada, reserva.Estado);
    }

    [Fact]
    public async Task CancelarPorCodigoAsync_UnMinutoDespuesDelLimite_Devuelve409ConDuracionLegible()
    {
        var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Ana Pérez", Telefono = "8095551234" };
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(), Codigo = "12345678", ClienteId = cliente.Id,
            FranjaHorariaId = Almuerzo.Id, Fecha = Fecha, Personas = 2
        };
        var reservaRepo = new ReservaRepositoryFake
        {
            ReservaPorCodigoYTelefono = reserva,
            ClienteDeLaReservaPorCodigoYTelefono = cliente
        };
        // "Ahora" son las 10:01 local: 1 minuto más tarde que el límite de 2h (quedan 1h59m).
        var ahoraUtc = new DateTimeOffset(Fecha.Year, Fecha.Month, Fecha.Day, 14, 1, 0, TimeSpan.Zero); // 10:01 RD
        var servicio = CrearServicio(reservaRepo: reservaRepo, ahoraUtc: ahoraUtc);

        var resultado = await servicio.CancelarPorCodigoAsync("12345678", new CancelarReservaRequest("8095551234"), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(GIRA.Application.Common.ErrorType.Conflict, resultado.Type);
        Assert.Contains("2 horas", resultado.Error);
        Assert.Equal(EstadoReserva.Confirmada, reserva.Estado);
    }

    [Fact]
    public async Task CancelarPorCodigoAsync_BienAntesDelLimite_PermiteCancelar()
    {
        var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Ana Pérez", Telefono = "8095551234" };
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(), Codigo = "12345678", ClienteId = cliente.Id,
            FranjaHorariaId = Almuerzo.Id, Fecha = Fecha, Personas = 2
        };
        var reservaRepo = new ReservaRepositoryFake
        {
            ReservaPorCodigoYTelefono = reserva,
            ClienteDeLaReservaPorCodigoYTelefono = cliente
        };
        // "Ahora" por defecto de CrearServicio: la mañana del día anterior, con antelación de
        // sobra respecto al límite de 2h.
        var servicio = CrearServicio(reservaRepo: reservaRepo);

        var resultado = await servicio.CancelarPorCodigoAsync("12345678", new CancelarReservaRequest("8095551234"), default);

        Assert.True(resultado.IsSuccess);
        Assert.Equal(EstadoReserva.Cancelada, reserva.Estado);
    }

    [Fact]
    public async Task CancelarPorCodigoAsync_ReservaNoConfirmada_Devuelve409()
    {
        var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Ana Pérez", Telefono = "8095551234" };
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(), Codigo = "12345678", ClienteId = cliente.Id,
            FranjaHorariaId = Almuerzo.Id, Fecha = Fecha, Personas = 2
        };
        reserva.Cancelar(null); // ya estaba cancelada antes de este intento
        var reservaRepo = new ReservaRepositoryFake
        {
            ReservaPorCodigoYTelefono = reserva,
            ClienteDeLaReservaPorCodigoYTelefono = cliente
        };
        var servicio = CrearServicio(reservaRepo: reservaRepo);

        var resultado = await servicio.CancelarPorCodigoAsync("12345678", new CancelarReservaRequest("8095551234"), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(GIRA.Application.Common.ErrorType.Conflict, resultado.Type);
        Assert.Equal("Esta reserva ya no se puede cancelar.", resultado.Error);
    }

    [Fact]
    public async Task CancelarPorCodigoAsync_TelefonoEquivocado_NoModificaLaReserva()
    {
        var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Ana Pérez", Telefono = "8095551234" };
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(), Codigo = "12345678", ClienteId = cliente.Id,
            FranjaHorariaId = Almuerzo.Id, Fecha = Fecha, Personas = 2
        };
        var reservaRepo = new ReservaRepositoryFake
        {
            ReservaPorCodigoYTelefono = reserva,
            ClienteDeLaReservaPorCodigoYTelefono = cliente
        };
        var servicio = CrearServicio(reservaRepo: reservaRepo);

        var resultado = await servicio.CancelarPorCodigoAsync("12345678", new CancelarReservaRequest("8299999999"), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(GIRA.Application.Common.ErrorType.NotFound, resultado.Type);
        Assert.Equal(EstadoReserva.Confirmada, reserva.Estado);
    }

    [Fact]
    public async Task CancelarPorCodigoAsync_Exito_PublicaElEventoSinDatosPersonales()
    {
        var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Ana Pérez", Telefono = "8095551234" };
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(), Codigo = "12345678", ClienteId = cliente.Id,
            FranjaHorariaId = Almuerzo.Id, Fecha = Fecha, Personas = 2
        };
        var reservaRepo = new ReservaRepositoryFake
        {
            ReservaPorCodigoYTelefono = reserva,
            ClienteDeLaReservaPorCodigoYTelefono = cliente
        };
        var publisher = new PublisherFalso();
        var servicio = CrearServicio(reservaRepo: reservaRepo, publisher: publisher);

        var resultado = await servicio.CancelarPorCodigoAsync("12345678", new CancelarReservaRequest("8095551234"), default);

        Assert.True(resultado.IsSuccess);
        var evento = Assert.Single(publisher.Publicados);
        var canceladoEvento = Assert.IsType<ReservaCanceladaEvent>(evento);
        Assert.Equal(new ReservaCanceladaEvent(reserva.Id, Fecha, Almuerzo.Id, 2), canceladoEvento);
    }

    [Fact]
    public async Task CancelarPorCodigoAsync_ConflictoDeConcurrenciaAlGuardar_Devuelve409SinPublicarElEvento()
    {
        var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Ana Pérez", Telefono = "8095551234" };
        var reserva = new Reserva
        {
            Id = Guid.NewGuid(), Codigo = "12345678", ClienteId = cliente.Id,
            FranjaHorariaId = Almuerzo.Id, Fecha = Fecha, Personas = 2
        };
        var reservaRepo = new ReservaRepositoryFake
        {
            ReservaPorCodigoYTelefono = reserva,
            ClienteDeLaReservaPorCodigoYTelefono = cliente,
            LanzarConflictoConcurrenciaAlGuardar = true
        };
        var publisher = new PublisherFalso();
        var servicio = CrearServicio(reservaRepo: reservaRepo, publisher: publisher);

        var resultado = await servicio.CancelarPorCodigoAsync("12345678", new CancelarReservaRequest("8095551234"), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(GIRA.Application.Common.ErrorType.Conflict, resultado.Type);
        Assert.Empty(publisher.Publicados);
    }

    [Theory]
    [InlineData(OrigenReserva.Web)]
    [InlineData((OrigenReserva)99)]
    public async Task CrearComoPersonalAsync_OrigenNoPermitido_DevuelveValidationSinGuardarNiPublicar(OrigenReserva origen)
    {
        var reservaRepo = new ReservaRepositoryFake();
        var publisher = new PublisherFalso();
        var servicio = CrearServicio(reservaRepo: reservaRepo, publisher: publisher);

        var resultado = await servicio.CrearComoPersonalAsync(Request(), origen, default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(GIRA.Application.Common.ErrorType.Validation, resultado.Type);
        Assert.Empty(reservaRepo.Agregadas);
        Assert.Empty(publisher.Publicados);
    }

    // --- GI-403: canales. El personal no tiene la antelación de 2 h; la web sí. ---

    private static readonly Mesa MesaGrande = new() { Id = Guid.NewGuid(), Numero = 2, Capacidad = 8 };

    // Hora local de Santo Domingo (UTC-4) del día de la reserva.
    private static DateTimeOffset LocalRd(int hora, int minuto, DateOnly? dia = null)
    {
        var d = dia ?? Fecha;
        return new DateTimeOffset(d.Year, d.Month, d.Day, hora + 4, minuto, 0, TimeSpan.Zero);
    }

    [Theory]
    [InlineData(OrigenReserva.Telefono)]
    [InlineData(OrigenReserva.Presencial)]
    public async Task CrearComoPersonal_Para_Hoy_A_Menos_De_2h_Del_Inicio_Si_Crea(OrigenReserva origen)
    {
        var servicio = CrearServicio(ahoraUtc: LocalRd(11, 0)); // la franja empieza a las 12:00

        var resultado = await servicio.CrearComoPersonalAsync(Request(), origen, default);

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public async Task CrearComoPersonal_Con_La_Franja_En_Curso_Si_Crea()
    {
        var servicio = CrearServicio(ahoraUtc: LocalRd(13, 0)); // 12:00-15:00 ya empezó

        var resultado = await servicio.CrearComoPersonalAsync(Request(), OrigenReserva.Presencial, default);

        Assert.True(resultado.IsSuccess);
    }

    [Fact]
    public async Task CrearAsync_Web_Justo_A_Las_2h_Pasa_Y_Un_Minuto_Antes_Falla()
    {
        var justo = await CrearServicio(ahoraUtc: LocalRd(10, 0)).CrearAsync(Request(), default);
        var unMinutoTarde = await CrearServicio(ahoraUtc: LocalRd(10, 1)).CrearAsync(Request(), default);

        Assert.True(justo.IsSuccess);
        Assert.True(unMinutoTarde.IsFailure);
        Assert.Equal("Esa franja ya no tiene la antelación mínima requerida.", unMinutoTarde.Error);
    }

    [Fact]
    public async Task CrearAsync_Web_Con_La_Franja_En_Curso_Sigue_Fallando()
    {
        var resultado = await CrearServicio(ahoraUtc: LocalRd(13, 0)).CrearAsync(Request(), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal(ErrorType.Validation, resultado.Type);
    }

    [Fact]
    public async Task CrearComoPersonal_Franja_Terminada_Falla_Y_Un_Minuto_Antes_De_Terminar_Pasa()
    {
        var terminada = await CrearServicio(ahoraUtc: LocalRd(15, 0))
            .CrearComoPersonalAsync(Request(), OrigenReserva.Telefono, default);
        var porTerminar = await CrearServicio(ahoraUtc: LocalRd(14, 59))
            .CrearComoPersonalAsync(Request(), OrigenReserva.Telefono, default);

        Assert.True(terminada.IsFailure);
        Assert.Equal(ErrorType.Validation, terminada.Type);
        Assert.Equal("Esa franja ya terminó; elige una que aún no haya terminado.", terminada.Error);
        Assert.True(porTerminar.IsSuccess);
    }

    [Fact]
    public async Task CrearComoPersonal_Fecha_Pasada_Falla()
    {
        var servicio = CrearServicio(ahoraUtc: LocalRd(8, 0, Fecha.AddDays(1)));

        var resultado = await servicio.CrearComoPersonalAsync(Request(), OrigenReserva.Telefono, default);

        Assert.True(resultado.IsFailure);
        Assert.Equal("La fecha no puede ser en el pasado.", resultado.Error);
    }

    [Fact]
    public async Task CrearComoPersonal_Nueve_Personas_Con_Mesa_Maxima_De_8_Falla_Con_Voz_De_Personal_Y_Ocho_Pasa()
    {
        var nueve = await CrearServicio(mesas: [MesaGrande]).CrearComoPersonalAsync(
            Request(personas: 9), OrigenReserva.Telefono, default);
        var ocho = await CrearServicio(mesas: [MesaGrande]).CrearComoPersonalAsync(
            Request(personas: 8), OrigenReserva.Telefono, default);

        Assert.True(nueve.IsFailure);
        Assert.Equal(ErrorType.Validation, nueve.Type);
        Assert.Equal(
            "Las mesas admiten como máximo 8 personas. Los grupos más grandes se atienden por teléfono, fuera del sistema.",
            nueve.Error);
        Assert.True(ocho.IsSuccess);
    }

    [Fact]
    public async Task CrearAsync_Web_Nueve_Personas_Conserva_El_Mensaje_Del_Cliente()
    {
        var resultado = await CrearServicio(mesas: [MesaGrande]).CrearAsync(Request(personas: 9), default);

        Assert.True(resultado.IsFailure);
        Assert.Equal("Para grupos de más de 8 personas, llama al restaurante.", resultado.Error);
    }

    [Fact]
    public async Task Limite_De_2_Vigentes_Usa_La_Voz_De_Cada_Audiencia()
    {
        var personal = await CrearServicio(reservaRepo: new ReservaRepositoryFake { VigentesPorTelefono = 2 })
            .CrearComoPersonalAsync(Request(), OrigenReserva.Telefono, default);
        var web = await CrearServicio(reservaRepo: new ReservaRepositoryFake { VigentesPorTelefono = 2 })
            .CrearAsync(Request(), default);

        Assert.Equal(ErrorType.Conflict, personal.Type);
        Assert.Equal("Este teléfono ya tiene 2 reservas vigentes; cancela una para registrar otra.", personal.Error);
        Assert.Equal("Ya tienes 2 reservas vigentes con este teléfono; cancela una para reservar otra.", web.Error);
    }

    [Fact]
    public async Task Telefono_Y_Presencial_Se_Comportan_Igual()
    {
        var porLlamada = await CrearServicio(ahoraUtc: LocalRd(11, 0))
            .CrearComoPersonalAsync(Request(), OrigenReserva.Telefono, default);
        var enPersona = await CrearServicio(ahoraUtc: LocalRd(11, 0))
            .CrearComoPersonalAsync(Request(), OrigenReserva.Presencial, default);
        var terminadaLlamada = await CrearServicio(ahoraUtc: LocalRd(16, 0))
            .CrearComoPersonalAsync(Request(), OrigenReserva.Telefono, default);
        var terminadaPersona = await CrearServicio(ahoraUtc: LocalRd(16, 0))
            .CrearComoPersonalAsync(Request(), OrigenReserva.Presencial, default);

        Assert.Equal(porLlamada.IsSuccess, enPersona.IsSuccess);
        Assert.Equal(terminadaLlamada.Error, terminadaPersona.Error);
    }
}

class ReservaRepositoryFake : IReservaRepository
{
    public IReadOnlyList<Reserva> VigentesPorFecha { get; set; } = [];
    public int VigentesPorTelefono { get; set; }
    public bool CodigoSiempreExiste { get; set; }
    public bool LanzarConflictoAlGuardar { get; set; }
    public List<Reserva> Agregadas { get; } = [];
    public Reserva? ReservaPorCodigoYTelefono { get; set; }
    public Cliente? ClienteDeLaReservaPorCodigoYTelefono { get; set; }
    public string? UltimoCodigoRecibido { get; private set; }
    public string? UltimoTelefonoRecibido { get; private set; }
    public bool LanzarConflictoConcurrenciaAlGuardar { get; set; }

    public Task<IReadOnlyList<Reserva>> ListarVigentesPorFechaAsync(DateOnly fecha, CancellationToken cancellationToken) =>
        Task.FromResult(VigentesPorFecha);

    public Task<Reserva?> BuscarPorCodigoYTelefonoAsync(string codigo, string telefono, CancellationToken cancellationToken)
    {
        UltimoCodigoRecibido = codigo;
        UltimoTelefonoRecibido = telefono;

        var coincide = ReservaPorCodigoYTelefono is not null
            && ClienteDeLaReservaPorCodigoYTelefono is not null
            && ReservaPorCodigoYTelefono.Codigo == codigo
            && ClienteDeLaReservaPorCodigoYTelefono.Telefono == telefono;

        return Task.FromResult(coincide ? ReservaPorCodigoYTelefono : null);
    }

    public Task<PagedResult<ReservaListadoDto>> ListarAsync(DateOnly? fecha, EstadoReserva? estado, Guid? franjaId, string? telefono, int page, int pageSize, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<ReservaDetalleDto?> ObtenerDetalleAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<Reserva?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<IReadOnlyList<ReservaConFranja>> ListarPorEstadoHastaAsync(EstadoReserva estado, DateOnly fechaMaxima, int limite, CancellationToken cancellationToken) =>
        throw new NotImplementedException();
    public void Descartar(Reserva reserva) => throw new NotImplementedException();

    public Task<bool> ExisteVigenteAsync(Guid mesaId, DateOnly fecha, Guid franjaId, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public Task<int> ContarVigentesPorTelefonoAsync(string telefono, CancellationToken cancellationToken) =>
        Task.FromResult(VigentesPorTelefono);

    public Task<bool> ExisteCodigoAsync(string codigo, CancellationToken cancellationToken) =>
        Task.FromResult(CodigoSiempreExiste);

    public void Agregar(Reserva reserva) => Agregadas.Add(reserva);

    public Task GuardarCambiosAsync(CancellationToken cancellationToken)
    {
        if (LanzarConflictoConcurrenciaAlGuardar)
        {
            throw new ReservaConflictoConcurrenciaException(
                "La reserva cambió mientras se procesaba la cancelación; vuelve a consultarla.", new InvalidOperationException());
        }

        if (LanzarConflictoAlGuardar)
        {
            throw new ReservaDuplicadaException("Esa mesa ya fue tomada, intenta otra franja.", new InvalidOperationException());
        }

        return Task.CompletedTask;
    }
}

class ClienteRepositoryFake : IClienteRepository
{
    private readonly Queue<Cliente?> _resultadosBusqueda;

    public bool LanzarDuplicadoAlGuardar { get; set; }
    public Cliente? Agregado { get; private set; }
    public Cliente? ClientePorId { get; set; }

    public ClienteRepositoryFake(params Cliente?[] resultadosBusqueda)
    {
        _resultadosBusqueda = new Queue<Cliente?>(resultadosBusqueda);
    }

    public Task<Cliente?> BuscarPorTelefonoAsync(string telefono, CancellationToken cancellationToken) =>
        Task.FromResult(_resultadosBusqueda.Count > 0 ? _resultadosBusqueda.Dequeue() : null);

    public Task<Cliente?> BuscarPorIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(ClientePorId);

    public Task<PagedResult<Cliente>> BuscarAsync(string texto, int page, int pageSize, CancellationToken cancellationToken) =>
        throw new NotImplementedException();

    public void Agregar(Cliente cliente) => Agregado = cliente;

    public Task GuardarCambiosAsync(CancellationToken cancellationToken)
    {
        if (LanzarDuplicadoAlGuardar)
        {
            throw new ClienteDuplicadoException("Otra solicitud ya creó un cliente con este teléfono.", new InvalidOperationException());
        }

        return Task.CompletedTask;
    }
}

class PublisherFalso : IPublisher
{
    public List<object> Publicados { get; } = [];

    public Task Publish(object notification, CancellationToken cancellationToken = default)
    {
        Publicados.Add(notification);
        return Task.CompletedTask;
    }

    public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
        where TNotification : INotification
    {
        Publicados.Add(notification!);
        return Task.CompletedTask;
    }
}
