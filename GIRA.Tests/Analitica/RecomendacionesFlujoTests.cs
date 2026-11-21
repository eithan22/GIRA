using System.Collections.Concurrent;
using System.Net.Http.Json;
using GIRA.Application.DTOs.Analitica;
using GIRA.Application.Events.Analitica;
using GIRA.Application.Interfaces.Services.Analitica;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Domain.Constants;
using GIRA.Domain.Entities.Analitica;
using GIRA.Domain.Entities.Inventario;
using GIRA.Domain.Entities.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Tests.Api;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GIRA.Tests.Analitica;

public sealed class RecomendacionesFlujoTests
{
    [Fact]
    public async Task Reglas_UsanDatosDeFuentesYSePuedenConsultarEnLaApi()
    {
        await using var fabrica = new Fabrica();
        using var cliente = fabrica.CreateClient();
        var franja = Guid.NewGuid();
        var insumo = Guid.NewGuid();
        var hoy = new DateOnly(2026, 11, 20);
        using (var scope = fabrica.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
            db.FranjasHorarias.Add(new FranjaHoraria { Id = franja, Nombre = "Almuerzo" });
            db.Insumos.Add(new Insumo { Id = insumo, Nombre = "Arroz" });
            for (var i = 0; i < 4; i++)
                db.Set<RegistroAfluencia>().Add(new RegistroAfluencia
                {
                    Id = Guid.NewGuid(), ReferenciaId = Guid.NewGuid(), Fecha = hoy.AddDays(-6),
                    FranjaHorariaId = franja, Origen = OrigenRegistroAfluencia.Reserva,
                    Tipo = i == 0 ? TipoRegistroAfluencia.ReservaNoAsistida : TipoRegistroAfluencia.VisitaCompletada,
                    Personas = i == 0 ? 1 : i == 1 ? 8 : 1
                });
            db.Set<PrediccionAfluencia>().Add(new PrediccionAfluencia
            {
                Id = Guid.NewGuid(), GeneracionId = Guid.NewGuid(), Fecha = hoy.AddDays(1), FranjaHorariaId = franja,
                AfluenciaEstimada = 13, BajaConfianza = true, VersionModelo = "prueba", Vigente = true,
                GeneradaEn = new DateTimeOffset(2026, 11, 20, 12, 0, 0, TimeSpan.Zero)
            });
            for (var i = 0; i < 3; i++)
                db.AlertasInventario.Add(new AlertaInventario
                {
                    Id = Guid.NewGuid(), InsumoId = insumo, FechaGeneracion = new DateTime(2026, 11, 19, 12, 0, 0, DateTimeKind.Utc)
                });
            await db.SaveChangesAsync();
        }
        using (var scope = fabrica.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IRecomendacionesService>().ActualizarAsync(default);
            var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
            cliente.UsarToken(tokens.GenerarAccessToken(Guid.NewGuid(), "personal@gira.local", "Personal", [Roles.Gerente]).Token);
        }

        var recomendaciones = await cliente.GetFromJsonAsync<List<RecomendacionDto>>("/api/analitica/recomendaciones?estado=Nueva");
        Assert.Equal(4, recomendaciones!.Count);
        Assert.Contains(recomendaciones, r => r.Tipo == "Personal");
        Assert.Contains(recomendaciones, r => r.Tipo == "Reservas");
        Assert.Contains(recomendaciones, r => r.Tipo == "Inventario" && r.Mensaje.Contains("Arroz"));
        Assert.Contains(recomendaciones, r => r.Tipo == "Analítica");
        var eventos = fabrica.Services.GetRequiredService<RecomendacionGeneradaEventRecorder>().Eventos;
        Assert.Equal(4, eventos.Count);
        Assert.All(recomendaciones, recomendacion =>
            Assert.Contains(eventos, evento => evento.RecomendacionId == recomendacion.Id));

        using (var scope = fabrica.Services.CreateScope())
            await scope.ServiceProvider.GetRequiredService<IRecomendacionesService>().ActualizarAsync(default);
        Assert.Equal(4, eventos.Count);
    }

    private sealed class Fabrica : GiraApiFactory
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new Reloj());
                services.AddSingleton<RecomendacionGeneradaEventRecorder>();
                services.AddTransient<INotificationHandler<RecomendacionGeneradaEvent>, RecomendacionGeneradaEventHandler>();
            });
        }
    }

    private sealed class RecomendacionGeneradaEventRecorder
    {
        public ConcurrentQueue<RecomendacionGeneradaEvent> Eventos { get; } = new();
    }

    private sealed class RecomendacionGeneradaEventHandler(RecomendacionGeneradaEventRecorder recorder)
        : INotificationHandler<RecomendacionGeneradaEvent>
    {
        public Task Handle(RecomendacionGeneradaEvent notification, CancellationToken cancellationToken)
        {
            recorder.Eventos.Enqueue(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class Reloj : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 21, 3, 0, 0, TimeSpan.Zero);
    }
}
