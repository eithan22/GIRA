using System.Net.Http.Json;
using GIRA.Application.DTOs.Analitica;
using GIRA.Application.Events.Analitica;
using GIRA.Application.Interfaces.Services.Analitica;
using GIRA.Application.Interfaces.Services.Seguridad.Auth;
using GIRA.Domain.Constants;
using GIRA.Domain.Entities.Analitica;
using GIRA.Domain.Entities.Reservas;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Tests.Api;
using MediatR;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace GIRA.Tests.Analitica;

public sealed class GeneracionPrediccionFlujoTests
{
    [Fact]
    public async Task PrediccionCompleta_LlegaAlEndpointYAlConsumidorDeInventario()
    {
        await using var fabrica = new Fabrica();
        using var cliente = fabrica.CreateClient();
        var franja = Guid.NewGuid();
        var hoy = new DateOnly(2026, 11, 20);
        using (var scope = fabrica.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<GiraDbContext>();
            db.FranjasHorarias.Add(new FranjaHoraria { Id = franja, Nombre = "Almuerzo", Activa = true });
            db.Set<RegistroAfluencia>().Add(new RegistroAfluencia
            {
                Id = Guid.NewGuid(), ReferenciaId = Guid.NewGuid(), Origen = OrigenRegistroAfluencia.Reserva,
                Tipo = TipoRegistroAfluencia.VisitaCompletada, Fecha = hoy.AddDays(-6),
                FranjaHorariaId = franja, Personas = 8
            });
            await db.SaveChangesAsync();
        }

        using (var scope = fabrica.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IGeneracionPrediccionService>().EjecutarAsync(default);
            var tokens = scope.ServiceProvider.GetRequiredService<ITokenService>();
            cliente.UsarToken(tokens.GenerarAccessToken(Guid.NewGuid(), "personal@gira.local", "Personal", [Roles.Gerente]).Token);
        }

        var respuesta = await cliente.GetFromJsonAsync<List<PrediccionAfluenciaDto>>(
            "/api/analitica/prediccion?desde=2026-11-21&hasta=2026-11-21");
        var prediccion = Assert.Single(respuesta!);
        Assert.Equal(8, prediccion.AfluenciaEstimada);
        Assert.True(prediccion.BajaConfianza);
        using var verificacion = fabrica.Services.CreateScope();
        var contexto = verificacion.ServiceProvider.GetRequiredService<GiraDbContext>();
        var recibida = await contexto.AfluenciasPredichas.SingleAsync(p => p.Fecha == hoy.AddDays(1));
        Assert.Equal(8, recibida.Comensales);
        Assert.All(await contexto.Set<PublicacionPrediccion>().ToListAsync(), p => Assert.NotNull(p.PublicadaEn));
        var evento = fabrica.Eventos.Eventos.Single(e => e.Desde == hoy.AddDays(1));
        var almacenada = await contexto.Set<PrediccionAfluencia>()
            .SingleAsync(p => p.Fecha == hoy.AddDays(1) && p.FranjaHorariaId == franja);
        var publicacion = await contexto.Set<PublicacionPrediccion>()
            .SingleAsync(p => p.Fecha == hoy.AddDays(1));
        var generacion = await contexto.Set<GeneracionPrediccion>()
            .SingleAsync(g => g.Id == publicacion.GeneracionId);
        Assert.Equal(almacenada.Id, Assert.Single(evento.Predicciones).PrediccionId);
        Assert.Equal(almacenada.VersionModelo, Assert.Single(evento.Predicciones).VersionModelo);
        Assert.Equal(Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"), evento.RestauranteId);
        Assert.Equal(publicacion.Id, evento.EventoId);
        Assert.Equal(1, evento.VersionContrato);
        Assert.Equal(generacion.GeneradaEn.ToUniversalTime(), evento.OcurridoEnUtc);
        Assert.Equal(nameof(PrediccionAfluenciaGeneradaEvent), evento.TipoEvento);
    }

    private sealed class Fabrica : GiraApiFactory
    {
        public Captura Eventos { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.UseSetting("Analitica:RestauranteId", "aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(new Reloj());
                services.AddSingleton<INotificationHandler<PrediccionAfluenciaGeneradaEvent>>(Eventos);
            });
        }
    }

    private sealed class Captura : INotificationHandler<PrediccionAfluenciaGeneradaEvent>
    {
        public List<PrediccionAfluenciaGeneradaEvent> Eventos { get; } = [];
        public Task Handle(PrediccionAfluenciaGeneradaEvent notification, CancellationToken cancellationToken)
        {
            Eventos.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class Reloj : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => new(2026, 11, 21, 3, 0, 0, TimeSpan.Zero);
    }
}
