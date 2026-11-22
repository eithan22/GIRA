using GIRA.Application.EventHandlers.Analitica;
using GIRA.Application.Events.Reservas;
using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Application.DTOs.Analitica;
using GIRA.Domain.Entities.Analitica;
using GIRA.Domain.Entities.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Tests.Api;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace GIRA.Tests.Analitica;

public sealed class AfluenciaPorReservasTests : IAsyncLifetime
{
    private readonly GiraApiFactory _factory = new();
    private readonly Guid _franja = Guid.NewGuid();
    private HttpClient _client = null!;

    public async Task InitializeAsync()
    {
        _client = _factory.CreateClient();
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        db.FranjasHorarias.Add(new FranjaHoraria { Id = _franja, Nombre = "Prueba" });
        await db.SaveChangesAsync();
    }

    public async Task DisposeAsync()
    {
        _client.Dispose();
        await _factory.DisposeAsync();
    }

    [Fact]
    public async Task VisitasYNoShows_SeGuardanUnaVez_YSinConfundirOrigenes()
    {
        var handler = CrearHandler();
        var fecha = new DateOnly(2026, 11, 18);
        var franja = _franja;
        var referencia = Guid.NewGuid();
        var visita = new VisitaCompletadaEvent(fecha, franja, 4, true, referencia);
        var walkIn = new VisitaCompletadaEvent(fecha, franja, 3, false, referencia);
        var noShow = new ReservaNoAsistidaEvent(Guid.NewGuid(), fecha, franja, 5);

        await handler.Handle(visita, CancellationToken.None);
        await handler.Handle(visita, CancellationToken.None);
        await handler.Handle(walkIn, CancellationToken.None);
        await handler.Handle(noShow, CancellationToken.None);
        await handler.Handle(noShow, CancellationToken.None);

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        var registros = await db.Set<RegistroAfluencia>().ToListAsync();
        Assert.Equal(3, registros.Count);
        Assert.All(registros, r =>
        {
            Assert.Equal(fecha, r.Fecha);
            Assert.Equal(franja, r.FranjaHorariaId);
        });
        Assert.Contains(registros, r => r.Origen == OrigenRegistroAfluencia.Reserva &&
                                       r.Tipo == TipoRegistroAfluencia.VisitaCompletada && r.Personas == 4);
        Assert.Contains(registros, r => r.Origen == OrigenRegistroAfluencia.VisitaSinReserva && r.Personas == 3);
        Assert.Contains(registros, r => r.Tipo == TipoRegistroAfluencia.ReservaNoAsistida && r.Personas == 5);
    }

    [Fact]
    public async Task EventoContradictorio_NoSobrescribeElHechoExistente()
    {
        var logger = new LoggerPrueba();
        var handler = CrearHandler(logger);
        var fecha = new DateOnly(2026, 11, 18);
        var franja = _franja;
        var id = Guid.NewGuid();
        await handler.Handle(new VisitaCompletadaEvent(fecha, franja, 4, true, id), CancellationToken.None);

        await handler.Handle(new ReservaNoAsistidaEvent(id, fecha, franja, 4), CancellationToken.None);
        Assert.IsType<InvalidOperationException>(Assert.Single(logger.Errores));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        Assert.Equal(TipoRegistroAfluencia.VisitaCompletada, (await db.Set<RegistroAfluencia>().SingleAsync()).Tipo);
    }

    [Fact]
    public async Task EventoInvalido_NoSeGuarda()
    {
        var logger = new LoggerPrueba();
        var handler = CrearHandler(logger);
        var evento = new VisitaCompletadaEvent(new DateOnly(2026, 11, 18), _franja, 0, true, Guid.NewGuid());

        await handler.Handle(evento, CancellationToken.None);
        Assert.IsType<ArgumentException>(Assert.Single(logger.Errores));

        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        Assert.Empty(await db.Set<RegistroAfluencia>().ToListAsync());
    }

    [Theory]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task FechaOFranjaAusente_NoSeGuarda(bool fechaAusente, bool franjaAusente)
    {
        var logger = new LoggerPrueba();
        var evento = new VisitaCompletadaEvent(
            fechaAusente ? default : new DateOnly(2026, 11, 18),
            franjaAusente ? Guid.Empty : _franja, 2, true, Guid.NewGuid());

        await CrearHandler(logger).Handle(evento, CancellationToken.None);
        Assert.IsType<ArgumentException>(Assert.Single(logger.Errores));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        Assert.Empty(await db.Set<RegistroAfluencia>().ToListAsync());
    }

    [Fact]
    public async Task FranjaDesconocida_NoSeGuardaYSeRegistraElError()
    {
        var logger = new LoggerPrueba();
        var handler = CrearHandler(logger);
        var evento = new VisitaCompletadaEvent(new DateOnly(2026, 11, 18), Guid.NewGuid(), 2, true, Guid.NewGuid());

        await handler.Handle(evento, CancellationToken.None);
        Assert.IsType<InvalidOperationException>(Assert.Single(logger.Errores));
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
        Assert.Empty(await db.Set<RegistroAfluencia>().ToListAsync());
    }

    [Fact]
    public async Task FranjaInactiva_ConservaEventosHistoricosValidos()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
            (await db.FranjasHorarias.SingleAsync(f => f.Id == _franja)).Activa = false;
            await db.SaveChangesAsync();
        }

        await CrearHandler().Handle(
            new ReservaNoAsistidaEvent(Guid.NewGuid(), new DateOnly(2026, 11, 18), _franja, 2),
            CancellationToken.None);

        using var verificacion = _factory.Services.CreateScope();
        var contexto = verificacion.ServiceProvider.GetRequiredService<GiraDbContext>();
        Assert.Single(await contexto.Set<RegistroAfluencia>().ToListAsync());
    }

    [Fact]
    public async Task FalloDeAfluencia_SeRegistraYNoInterrumpeElSiguienteHandler()
    {
        var logger = new LoggerPrueba();
        var siguiente = new HandlerPosterior();
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(typeof(AfluenciaPorReservasHandler).Assembly));
        servicios.RemoveAll<INotificationHandler<VisitaCompletadaEvent>>();
        servicios.AddScoped<IRegistroAfluenciaRepository, RepositorioConFallo>();
        servicios.AddSingleton<ILogger<AfluenciaPorReservasHandler>>(logger);
        servicios.AddTransient<INotificationHandler<VisitaCompletadaEvent>, AfluenciaPorReservasHandler>();
        servicios.AddSingleton<INotificationHandler<VisitaCompletadaEvent>>(siguiente);
        using var proveedor = servicios.BuildServiceProvider();
        using var scope = proveedor.CreateScope();
        var evento = new VisitaCompletadaEvent(new DateOnly(2026, 11, 18), _franja, 2, true, Guid.NewGuid());

        await scope.ServiceProvider.GetRequiredService<IPublisher>().Publish(evento);

        Assert.IsType<InvalidOperationException>(Assert.Single(logger.Errores));
        Assert.Equal(evento, Assert.Single(siguiente.Eventos));
    }

    private sealed class RepositorioConFallo : IRegistroAfluenciaRepository
    {
        public Task RegistrarAsync(RegistroAfluencia registro, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Fallo de persistencia de prueba.");

        public Task<IReadOnlyList<HistorialAfluenciaDto>> ConsultarAsync(
            DateOnly desde, DateOnly hasta, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private sealed class HandlerPosterior : INotificationHandler<VisitaCompletadaEvent>
    {
        public List<VisitaCompletadaEvent> Eventos { get; } = [];

        public Task Handle(VisitaCompletadaEvent notification, CancellationToken cancellationToken)
        {
            Eventos.Add(notification);
            return Task.CompletedTask;
        }
    }

    private AfluenciaPorReservasHandler CrearHandler(ILogger<AfluenciaPorReservasHandler>? logger = null) =>
        new(_factory.Services.GetRequiredService<IServiceScopeFactory>(), logger ?? NullLogger<AfluenciaPorReservasHandler>.Instance);

    private sealed class LoggerPrueba : ILogger<AfluenciaPorReservasHandler>
    {
        public List<Exception?> Errores { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => logLevel == LogLevel.Error;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (logLevel == LogLevel.Error)
            {
                Errores.Add(exception);
            }
        }
    }
}
