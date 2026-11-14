using System.Collections.Concurrent;
using GIRA.Application.Events.Reservas;
using GIRA.Application.Services.Reservas;
using GIRA.Domain.Entities.Reservas;
using GIRA.Domain.Enums.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Repositories.Reservas;
using GIRA.Infrastructure.Services.Reservas;
using GIRA.Tests.Common;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;

namespace GIRA.Tests.Reservas;

/// <summary>
/// Antes de cualquier prueba de la clase, borra y vuelve a migrar GIRA_GI161_TEST una sola vez, para que
/// la corrida empiece siempre vacía. Mismo patrón que <see cref="ReservaServiceIntegrationFixture"/>, con
/// su propia base para no pisar a las demás pruebas de integración que corren en paralelo. Requiere el
/// User Secret <c>ConnectionStrings:GiraGi161Test</c> en GIRA.Tests (nunca GIRA_DB).
/// </summary>
public class ProcesoNoShowsIntegrationFixture : IAsyncLifetime
{
    /// <summary>Opciones de EF ya verificadas contra la base de pruebas.</summary>
    public DbContextOptions<GiraDbContext> Opciones { get; private set; } = null!;

    /// <inheritdoc />
    public async Task InitializeAsync()
    {
        var configuracion = new ConfigurationBuilder()
            .AddUserSecrets<ProcesoNoShowsIntegrationFixture>()
            .Build();

        var conexion = configuracion.GetConnectionString("GiraGi161Test");
        if (string.IsNullOrWhiteSpace(conexion))
        {
            throw new InvalidOperationException(
                "Falta la cadena de conexión 'GiraGi161Test' en los User Secrets de GIRA.Tests.");
        }

        // Seguro: solo se borra si el nombre de la base es, exactamente, la de pruebas de GI-161.
        var datosConexion = new Microsoft.Data.SqlClient.SqlConnectionStringBuilder(conexion);
        if (!string.Equals(datosConexion.InitialCatalog, "GIRA_GI161_TEST", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Las pruebas GI-161 solo pueden utilizar GIRA_GI161_TEST.");
        }

        Opciones = new DbContextOptionsBuilder<GiraDbContext>().UseSqlServer(conexion).Options;
        await using var db = new GiraDbContext(Opciones);
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    /// <inheritdoc />
    public Task DisposeAsync() => Task.CompletedTask;
}

/// <summary>
/// Dos ejecuciones simultáneas del proceso de no-shows contra SQL Server real (GI-161, GI-231): el
/// <c>RowVersion</c> de la reserva hace que solo una la marque y publique el evento; la otra la omite.
/// </summary>
[Trait("Category", "Integration")]
public class ProcesoNoShowsIntegrationTests : IClassFixture<ProcesoNoShowsIntegrationFixture>
{
    private static readonly Guid FranjaAlmuerzoId = Guid.Parse("00000000-0000-0000-0000-000000000001");

    private readonly ProcesoNoShowsIntegrationFixture _fixture;

    /// <summary>Crea la clase con la base ya preparada por el fixture.</summary>
    public ProcesoNoShowsIntegrationTests(ProcesoNoShowsIntegrationFixture fixture) => _fixture = fixture;

    private sealed class PublisherConcurrente : IPublisher
    {
        public ConcurrentQueue<object> Publicados { get; } = new();

        public Task Publish(object notification, CancellationToken cancellationToken = default)
        {
            Publicados.Enqueue(notification);
            return Task.CompletedTask;
        }

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification
        {
            Publicados.Enqueue(notification!);
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Dos_Ejecuciones_Simultaneas_Marcan_La_Reserva_Una_Sola_Vez_Y_Publican_Un_Solo_Evento()
    {
        // Una reserva Confirmada de una fecha pasada (la franja Almuerzo 12:00-15:00 la siembra la migración).
        Guid reservaId;
        await using (var db = new GiraDbContext(_fixture.Opciones))
        {
            var zona = new Zona { Id = Guid.NewGuid(), Nombre = "Zona GI-161" };
            var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 16101, Capacidad = 4, ZonaId = zona.Id };
            var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Cliente GI-161", Telefono = "8095550161" };
            var reserva = new Reserva
            {
                Id = Guid.NewGuid(), Codigo = "16100001", ClienteId = cliente.Id, MesaId = mesa.Id,
                FranjaHorariaId = FranjaAlmuerzoId, Fecha = new DateOnly(2020, 1, 6), Personas = 3,
                Origen = OrigenReserva.Telefono
            };
            db.Zonas.Add(zona);
            db.Mesas.Add(mesa);
            db.Clientes.Add(cliente);
            db.Reservas.Add(reserva);
            await db.SaveChangesAsync();
            reservaId = reserva.Id;
        }

        // Dos "instancias" de la API: cada una con su contexto. La barrera obliga a las dos a haber leído la
        // reserva como Confirmada antes de que cualquiera guarde, o sea, una carrera real.
        var publisher = new PublisherConcurrente();
        var barrera = new Barrier(2);
        var reloj = new TimeProviderFalso(new DateTimeOffset(2026, 12, 1, 16, 0, 0, TimeSpan.Zero));

        ProcesoAutomaticoReservasService Instancia(GiraDbContext contexto) => new(
            new ReservaRepositoryConBarrera(new ReservaRepository(contexto), barrera),
            new ParametrosReservaFalso(),
            new PublicadorEventosSeguro(publisher, NullLogger<PublicadorEventosSeguro>.Instance),
            reloj);

        await using var contextoA = new GiraDbContext(_fixture.Opciones);
        await using var contextoB = new GiraDbContext(_fixture.Opciones);
        var a = Instancia(contextoA);
        var b = Instancia(contextoB);

        var resultados = await Task.WhenAll(
            Task.Run(() => a.MarcarNoShowsAsync(default)),
            Task.Run(() => b.MarcarNoShowsAsync(default)));

        Assert.Equal(1, resultados.Sum(r => r.NoAsistidas));
        Assert.Equal(1, resultados.Sum(r => r.Omitidas));
        Assert.All(resultados, r => Assert.Empty(r.Fallos));

        var evento = Assert.IsType<ReservaNoAsistidaEvent>(Assert.Single(publisher.Publicados));
        Assert.Equal(reservaId, evento.ReservaId);
        Assert.Equal(new DateOnly(2020, 1, 6), evento.Fecha);
        Assert.Equal(FranjaAlmuerzoId, evento.FranjaHorariaId);
        Assert.Equal(3, evento.Personas);

        await using var lectura = new GiraDbContext(_fixture.Opciones);
        Assert.Equal(EstadoReserva.NoAsistio, (await lectura.Reservas.SingleAsync(r => r.Id == reservaId)).Estado);
    }

    [Fact]
    public async Task Dos_Ejecuciones_Simultaneas_Completan_La_Reserva_Una_Sola_Vez_Y_Publican_Un_Solo_Evento()
    {
        // Una reserva EnCurso de una fecha pasada: reservada para 3 personas, llegaron 4 (mesa de 4).
        Guid reservaId;
        await using (var db = new GiraDbContext(_fixture.Opciones))
        {
            var zona = new Zona { Id = Guid.NewGuid(), Nombre = "Zona GI-224" };
            var mesa = new Mesa { Id = Guid.NewGuid(), Numero = 16102, Capacidad = 4, ZonaId = zona.Id };
            var cliente = new Cliente { Id = Guid.NewGuid(), Nombre = "Cliente GI-224", Telefono = "8095550162" };
            var reserva = new Reserva
            {
                Id = Guid.NewGuid(), Codigo = "16100002", ClienteId = cliente.Id, MesaId = mesa.Id,
                FranjaHorariaId = FranjaAlmuerzoId, Fecha = new DateOnly(2020, 1, 7), Personas = 3,
                Origen = OrigenReserva.Telefono
            };
            reserva.HacerCheckIn(4, DateTime.UtcNow);
            db.Zonas.Add(zona);
            db.Mesas.Add(mesa);
            db.Clientes.Add(cliente);
            db.Reservas.Add(reserva);
            await db.SaveChangesAsync();
            reservaId = reserva.Id;
        }

        // Dos "instancias" de la API: cada una con su contexto. La barrera obliga a las dos a haber leído la
        // reserva como EnCurso antes de que cualquiera guarde, o sea, una carrera real.
        var publisher = new PublisherConcurrente();
        var barrera = new Barrier(2);
        var reloj = new TimeProviderFalso(new DateTimeOffset(2026, 12, 1, 16, 0, 0, TimeSpan.Zero));

        ProcesoAutomaticoReservasService Instancia(GiraDbContext contexto) => new(
            new ReservaRepositoryConBarrera(new ReservaRepository(contexto), barrera),
            new ParametrosReservaFalso(),
            new PublicadorEventosSeguro(publisher, NullLogger<PublicadorEventosSeguro>.Instance),
            reloj);

        await using var contextoA = new GiraDbContext(_fixture.Opciones);
        await using var contextoB = new GiraDbContext(_fixture.Opciones);
        var a = Instancia(contextoA);
        var b = Instancia(contextoB);

        var resultados = await Task.WhenAll(
            Task.Run(() => a.CompletarVencidasAsync(default)),
            Task.Run(() => b.CompletarVencidasAsync(default)));

        Assert.Equal(1, resultados.Sum(r => r.Completadas));
        Assert.Equal(1, resultados.Sum(r => r.Omitidas));
        Assert.All(resultados, r => Assert.Empty(r.Fallos));

        var evento = Assert.IsType<VisitaCompletadaEvent>(Assert.Single(publisher.Publicados));
        Assert.Equal(new VisitaCompletadaEvent(new DateOnly(2020, 1, 7), FranjaAlmuerzoId, 4, ConReserva: true), evento);

        await using var lectura = new GiraDbContext(_fixture.Opciones);
        Assert.Equal(EstadoReserva.Completada, (await lectura.Reservas.SingleAsync(r => r.Id == reservaId)).Estado);
    }
}
