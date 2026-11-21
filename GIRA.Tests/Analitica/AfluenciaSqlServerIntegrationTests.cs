using GIRA.Application.EventHandlers.Analitica;
using GIRA.Application.Events.Analitica;
using GIRA.Application.Events.Reservas;
using GIRA.Application.Interfaces.Repository.Analitica;
using GIRA.Domain.Entities.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Repositories.Analitica;
using GIRA.Infrastructure.Services.Reservas;
using MediatR;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace GIRA.Tests.Analitica;

[Trait("Category", "Integration")]
public sealed class AfluenciaSqlServerIntegrationTests
{
    [Fact]
    public async Task ErrorDeSqlServer_SeRegistraYPropagaAlProductor()
    {
        var origen = new ConfigurationBuilder().AddUserSecrets<Program>().Build().GetConnectionString("GiraDb")
            ?? throw new InvalidOperationException("Falta ConnectionStrings:GiraDb en los secretos de la API.");
        var prefijo = "GIRA_ANALITICA_AFLUENCIA_ERROR_";
        var nombre = prefijo + Guid.NewGuid().ToString("N");
        var conexion = new SqlConnectionStringBuilder(origen) { InitialCatalog = nombre }.ConnectionString;
        var opciones = new DbContextOptionsBuilder<GiraDbContext>().UseSqlServer(conexion).Options;
        await using var db = new GiraDbContext(opciones);
        var creada = false;
        try
        {
            var master = new SqlConnectionStringBuilder(origen) { InitialCatalog = "master" };
            await using (var comprobacion = new SqlConnection(master.ConnectionString))
            {
                await comprobacion.OpenAsync();
                await using var comando = comprobacion.CreateCommand();
                comando.CommandText = $"CREATE DATABASE [{nombre}]";
                await comando.ExecuteNonQueryAsync();
                creada = true;
            }

            Assert.True(await db.Database.EnsureCreatedAsync());
            var franjaId = Guid.NewGuid();
            db.FranjasHorarias.Add(new FranjaHoraria { Id = franjaId, Nombre = "Franja de prueba" });
            await db.SaveChangesAsync();
            await db.Database.ExecuteSqlRawAsync("DROP TABLE [analitica].[RegistroAfluencia]");

            var servicios = new ServiceCollection();
            servicios.AddDbContext<GiraDbContext>(opcionesDb => opcionesDb.UseSqlServer(conexion));
            servicios.AddScoped<IRegistroAfluenciaRepository, RegistroAfluenciaRepository>();
            using var proveedor = servicios.BuildServiceProvider();
            var handler = new AfluenciaPorReservasHandler(
                proveedor.GetRequiredService<IServiceScopeFactory>(),
                NullLogger<AfluenciaPorReservasHandler>.Instance);
            var publicador = new PublicadorEventosSeguro(
                new PublicadorPrueba(handler),
                NullLogger<PublicadorEventosSeguro>.Instance);
            var evento = new VisitaCompletadaEvent(
                new DateOnly(2026, 11, 21), franjaId, 3, ConReserva: true, VisitaId: Guid.NewGuid());

            var error = await Assert.ThrowsAsync<ErrorRegistroAfluenciaException>(() =>
                publicador.PublicarAsync(evento, evento.VisitaId.ToString(), CancellationToken.None));

            Assert.IsType<SqlException>(error.InnerException);
        }
        finally
        {
            if (creada && nombre.StartsWith(prefijo, StringComparison.Ordinal)
                && Guid.TryParseExact(nombre[prefijo.Length..], "N", out _))
                await db.Database.EnsureDeletedAsync();
        }
    }

    private sealed class PublicadorPrueba(AfluenciaPorReservasHandler handler) : IPublisher
    {
        public Task Publish(object notification, CancellationToken cancellationToken = default) =>
            notification is VisitaCompletadaEvent visita
                ? handler.Handle(visita, cancellationToken)
                : Task.CompletedTask;

        public Task Publish<TNotification>(TNotification notification, CancellationToken cancellationToken = default)
            where TNotification : INotification =>
            notification is VisitaCompletadaEvent visita
                ? handler.Handle(visita, cancellationToken)
                : Task.CompletedTask;
    }
}
