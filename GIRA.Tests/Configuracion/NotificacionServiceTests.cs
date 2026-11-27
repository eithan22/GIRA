using GIRA.Application.Common;
using GIRA.Application.DTOs.Configuracion;
using GIRA.Application.Interfaces.Services.Configuracion;
using GIRA.Application.Services.Configuracion;
using GIRA.Domain.Enums.Configuracion;
using GIRA.Infrastructure.Persistence.Context;
using GIRA.Infrastructure.Persistence.Repositories.Configuracion;
using GIRA.Tests.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace GIRA.Tests.Configuracion;

/// <summary>
/// Reglas de <see cref="NotificacionService"/> (RF-CFG-05, RF-CFG-06, RN-CFG-05): se guarda aunque falle el tiempo real,
/// un usuario repetido recibe un solo aviso y nadie lee ni marca las notificaciones de otro.
/// </summary>
public class NotificacionServiceTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 26, 15, 0, 0, TimeSpan.Zero);
    private static readonly AvisoNuevo Aviso = new("AlertaInventario", "Stock bajo: Arroz", "Quedan 2 kg.");

    private sealed class EmisorFalso : IEmisorNotificaciones
    {
        public bool Fallar { get; set; }
        public List<Guid> Enviadas { get; } = [];

        public Task EnviarAsync(Guid usuarioId, NotificacionDto notificacion, CancellationToken cancellationToken)
        {
            if (Fallar)
            {
                throw new InvalidOperationException("SignalR caído");
            }
            Enviadas.Add(usuarioId);
            return Task.CompletedTask;
        }
    }

    private sealed class Entorno : IDisposable
    {
        public GiraDbContext Contexto { get; }
        public EmisorFalso Emisor { get; } = new();
        public NotificacionService Servicio { get; }

        public Entorno()
        {
            Contexto = new GiraDbContext(new DbContextOptionsBuilder<GiraDbContext>().UseInMemoryDatabase(Guid.NewGuid().ToString()).Options);
            Servicio = new NotificacionService(new NotificacionRepository(Contexto), Emisor, new TimeProviderFalso(Ahora),
                NullLogger<NotificacionService>.Instance);
        }

        public void Dispose() => Contexto.Dispose();
    }

    [Fact]
    public async Task Notificar_GuardaUnaPorUsuario_Y_LaMarcaEnviada()
    {
        using var e = new Entorno();
        var ana = Guid.NewGuid();
        var luis = Guid.NewGuid();

        await e.Servicio.NotificarAsync(
            [new(ana, "Gerente"), new(ana, "Administrador"), new(luis, "Supervisor")], Aviso, default);

        var guardadas = await e.Contexto.Notificaciones.ToListAsync();
        Assert.Equal(2, guardadas.Count);
        Assert.All(guardadas, n => Assert.Equal(EstadoNotificacion.Enviada, n.Estado));
        Assert.All(guardadas, n => Assert.Equal(Ahora.UtcDateTime, n.FechaEnvio));
        Assert.Equal("Gerente", guardadas.Single(n => n.UsuarioId == ana).Destinatario);
        Assert.Equal([ana, luis], e.Emisor.Enviadas);
    }

    [Fact]
    public async Task Notificar_SiFallaElTiempoReal_QuedaGuardadaComoFallida_Y_SaleEnLaBandeja()
    {
        using var e = new Entorno();
        e.Emisor.Fallar = true;
        var ana = Guid.NewGuid();

        await e.Servicio.NotificarAsync([new(ana, "Gerente")], Aviso, default);

        var guardada = await e.Contexto.Notificaciones.SingleAsync();
        Assert.Equal(EstadoNotificacion.Fallida, guardada.Estado);
        Assert.Equal(1, guardada.Intentos);
        var bandeja = await e.Servicio.ListarMiasAsync(ana, leidas: null, default);
        Assert.Equal(1, bandeja.NoLeidas);
        Assert.Equal("Stock bajo: Arroz", bandeja.Notificaciones.Single().Titulo);
    }

    [Fact]
    public async Task Notificar_SinDestinatarios_NoGuardaNada()
    {
        using var e = new Entorno();

        await e.Servicio.NotificarAsync([], Aviso, default);

        Assert.Empty(e.Contexto.Notificaciones);
    }

    [Fact]
    public async Task MarcarLeida_DeOtroUsuario_EsNotFound_Y_LaPropiaBajaElContador()
    {
        using var e = new Entorno();
        var ana = Guid.NewGuid();
        await e.Servicio.NotificarAsync([new(ana, "Gerente")], Aviso, default);
        var id = (await e.Contexto.Notificaciones.SingleAsync()).Id;

        var deOtro = await e.Servicio.MarcarLeidaAsync(id, Guid.NewGuid(), default);
        var propia = await e.Servicio.MarcarLeidaAsync(id, ana, default);

        Assert.Equal(ErrorType.NotFound, deOtro.Type);
        Assert.True(propia.IsSuccess);
        var bandeja = await e.Servicio.ListarMiasAsync(ana, leidas: false, default);
        Assert.Equal(0, bandeja.NoLeidas);
        Assert.Empty(bandeja.Notificaciones);
    }
}
