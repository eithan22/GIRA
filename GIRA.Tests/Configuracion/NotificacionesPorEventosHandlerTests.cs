using GIRA.Application.Common;
using GIRA.Application.DTOs.Configuracion;
using GIRA.Application.EventHandlers.Configuracion;
using GIRA.Application.Events.Inventario;
using GIRA.Application.Events.Personal;
using GIRA.Application.Interfaces.Services.Configuracion;
using GIRA.Domain.Constants;
using GIRA.Domain.Enums.Inventario;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace GIRA.Tests.Configuracion;

/// <summary>
/// Texto y destinatarios de los avisos que arma <see cref="NotificacionesPorEventosHandler"/> (SRS 5.6, «Notificaciones
/// que genera»). La consulta y el servicio de notificaciones son falsos.
/// </summary>
public class NotificacionesPorEventosHandlerTests
{
    private static readonly Guid Gerente = Guid.NewGuid();
    private static readonly Guid EmpleadoConCuenta = Guid.NewGuid();
    private static readonly Guid UsuarioDelEmpleado = Guid.NewGuid();

    private sealed class ConsultaFalsa : IConsultaNotificaciones
    {
        public IReadOnlyCollection<string>? RolesPedidos { get; private set; }

        public Task<IReadOnlyList<DestinatarioNotificacion>> UsuariosConRolesAsync(IReadOnlyCollection<string> roles, CancellationToken ct)
        {
            RolesPedidos = roles;
            return Task.FromResult<IReadOnlyList<DestinatarioNotificacion>>([new(Gerente, Roles.Gerente)]);
        }

        public Task<Guid?> UsuarioDelEmpleadoAsync(Guid empleadoId, CancellationToken ct) =>
            Task.FromResult(empleadoId == EmpleadoConCuenta ? UsuarioDelEmpleado : (Guid?)null);

        public Task<InsumoAviso?> InsumoAsync(Guid insumoId, CancellationToken ct) => Task.FromResult<InsumoAviso?>(new("Arroz", "kg"));
        public Task<string?> NombreFranjaAsync(Guid franjaId, CancellationToken ct) => Task.FromResult<string?>("Cena");
        public Task<string?> NombreCargoAsync(Guid cargoId, CancellationToken ct) => Task.FromResult<string?>("Mesero");
    }

    private sealed class NotificacionesFalsas : INotificacionService
    {
        public List<(IReadOnlyCollection<DestinatarioNotificacion> A, AvisoNuevo Aviso)> Enviadas { get; } = [];

        public Task NotificarAsync(IReadOnlyCollection<DestinatarioNotificacion> destinatarios, AvisoNuevo aviso, CancellationToken ct)
        {
            Enviadas.Add((destinatarios, aviso));
            return Task.CompletedTask;
        }

        public Task<BandejaNotificacionesDto> ListarMiasAsync(Guid usuarioId, bool? leidas, CancellationToken ct) => throw new NotSupportedException();
        public Task<OperationResult> MarcarLeidaAsync(Guid id, Guid usuarioId, CancellationToken ct) => throw new NotSupportedException();
    }

    private readonly ConsultaFalsa _consulta = new();
    private readonly NotificacionesFalsas _notificaciones = new();
    private readonly NotificacionesPorEventosHandler _handler;

    public NotificacionesPorEventosHandlerTests()
    {
        var proveedor = new ServiceCollection()
            .AddSingleton<IConsultaNotificaciones>(_consulta)
            .AddSingleton<INotificacionService>(_notificaciones)
            .BuildServiceProvider();
        _handler = new NotificacionesPorEventosHandler(
            proveedor.GetRequiredService<IServiceScopeFactory>(), NullLogger<NotificacionesPorEventosHandler>.Instance);
    }

    [Fact]
    public async Task QuiebreProyectado_AvisaAGerenciaYSupervision_ConInsumoStockYFecha()
    {
        await _handler.Handle(new AlertaInventarioGeneradaEvent(
            Guid.NewGuid(), Guid.NewGuid(), TipoAlertaInventario.QuiebreProyectado, 4.5m, 1m, new DateOnly(2026, 11, 28)), default);

        Assert.Equal([Roles.Administrador, Roles.Gerente, Roles.Supervisor], _consulta.RolesPedidos);
        var (a, aviso) = Assert.Single(_notificaciones.Enviadas);
        Assert.Equal(Gerente, Assert.Single(a).UsuarioId);
        Assert.Equal("Se acabará pronto: Arroz", aviso.Titulo);
        Assert.Equal("Quedan 4.5 kg. Con el consumo proyectado bajará del punto de reorden el 28/11/2026.", aviso.Mensaje);
    }

    [Theory]
    [InlineData(2, "Cena del 30/11/2026: faltan 2 personas de Mesero. Revisa «Sugerencias».")]
    [InlineData(-1, "Cena del 30/11/2026: sobran 1 persona de Mesero. Revisa «Sugerencias».")]
    public async Task Sugerencia_DiceSiFaltanOSobran(int diferencia, string esperado)
    {
        await _handler.Handle(new SugerenciaTurnoGeneradaEvent(Guid.NewGuid(), new DateOnly(2026, 11, 30), Guid.NewGuid(), Guid.NewGuid(), diferencia), default);

        Assert.Equal(esperado, Assert.Single(_notificaciones.Enviadas).Aviso.Mensaje);
    }

    [Fact]
    public async Task TurnoAsignado_AvisaSoloAlUsuarioDelEmpleado()
    {
        await _handler.Handle(new TurnoAsignadoEvent(Guid.NewGuid(), EmpleadoConCuenta, new DateOnly(2026, 12, 1), new TimeOnly(8, 0), new TimeOnly(16, 30)), default);

        var (a, aviso) = Assert.Single(_notificaciones.Enviadas);
        Assert.Equal(new DestinatarioNotificacion(UsuarioDelEmpleado, "Empleado"), Assert.Single(a));
        Assert.Equal("Tienes turno el 01/12/2026 de 08:00 a 16:30.", aviso.Mensaje);
    }

    [Fact]
    public async Task TurnoModificado_DeEmpleadoSinCuenta_NoAvisaANadie()
    {
        await _handler.Handle(new TurnoModificadoEvent(Guid.NewGuid(), Guid.NewGuid(), "Estado: Cancelado"), default);

        Assert.Empty(_notificaciones.Enviadas);
    }
}
