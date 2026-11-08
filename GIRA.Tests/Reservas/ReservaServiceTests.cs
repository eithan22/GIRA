using GIRA.Application.Common;
using GIRA.Application.DTOs.Reservas;
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
            parametros,
            validador,
            new GeneradorCodigoReserva(),
            publisher,
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
            parametros,
            validador,
            new GeneradorCodigoReserva(),
            new PublisherFalso(),
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
}

class ReservaRepositoryFake : IReservaRepository
{
    public IReadOnlyList<Reserva> VigentesPorFecha { get; set; } = [];
    public int VigentesPorTelefono { get; set; }
    public bool CodigoSiempreExiste { get; set; }
    public bool LanzarConflictoAlGuardar { get; set; }
    public List<Reserva> Agregadas { get; } = [];

    public Task<IReadOnlyList<Reserva>> ListarVigentesPorFechaAsync(DateOnly fecha, CancellationToken cancellationToken) =>
        Task.FromResult(VigentesPorFecha);

    public Task<int> ContarVigentesPorTelefonoAsync(string telefono, CancellationToken cancellationToken) =>
        Task.FromResult(VigentesPorTelefono);

    public Task<bool> ExisteCodigoAsync(string codigo, CancellationToken cancellationToken) =>
        Task.FromResult(CodigoSiempreExiste);

    public void Agregar(Reserva reserva) => Agregadas.Add(reserva);

    public Task GuardarCambiosAsync(CancellationToken cancellationToken)
    {
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

    public ClienteRepositoryFake(params Cliente?[] resultadosBusqueda)
    {
        _resultadosBusqueda = new Queue<Cliente?>(resultadosBusqueda);
    }

    public Task<Cliente?> BuscarPorTelefonoAsync(string telefono, CancellationToken cancellationToken) =>
        Task.FromResult(_resultadosBusqueda.Count > 0 ? _resultadosBusqueda.Dequeue() : null);

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
