using System.Globalization;
using GIRA.Application.DTOs.Configuracion;
using GIRA.Application.Events.Analitica;
using GIRA.Application.Events.Inventario;
using GIRA.Application.Events.Personal;
using GIRA.Application.Interfaces.Services.Configuracion;
using GIRA.Domain.Constants;
using GIRA.Domain.Enums.Inventario;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GIRA.Application.EventHandlers.Configuracion;

/// <summary>
/// Convierte los eventos de los demás módulos en avisos in-app (SRS 5.6, «Notificaciones que genera»). No tiene lógica
/// de negocio de esos módulos: solo arma el texto y elige a quién avisar. El Administrador recibe lo mismo que el Gerente.
/// </summary>
public class NotificacionesPorEventosHandler :
    INotificationHandler<AlertaInventarioGeneradaEvent>,
    INotificationHandler<TurnoAsignadoEvent>,
    INotificationHandler<TurnoModificadoEvent>,
    INotificationHandler<SugerenciaTurnoGeneradaEvent>,
    INotificationHandler<RecomendacionGeneradaEvent>,
    INotificationHandler<EntrenamientoModeloFallidoEvent>
{
    private static readonly string[] Gerencia = [Roles.Administrador, Roles.Gerente];
    private static readonly string[] GerenciaYSupervision = [Roles.Administrador, Roles.Gerente, Roles.Supervisor];
    private const string MotivoEmpleado = "Empleado";

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<NotificacionesPorEventosHandler> _logger;

    /// <summary>Crea el handler.</summary>
    public NotificacionesPorEventosHandler(IServiceScopeFactory scopes, ILogger<NotificacionesPorEventosHandler> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    /// <summary>Alerta de inventario: avisa a Gerente, Supervisor y Administrador.</summary>
    public Task Handle(AlertaInventarioGeneradaEvent e, CancellationToken ct) => AvisarRolesAsync(GerenciaYSupervision, async consulta =>
    {
        var insumo = await consulta.InsumoAsync(e.InsumoId, ct);
        var nombre = insumo?.Nombre ?? "un insumo";
        var stock = $"{Numero(e.StockActual)} {insumo?.Unidad}".TrimEnd();
        return e.Tipo switch
        {
            TipoAlertaInventario.StockBajo => new AvisoNuevo("AlertaInventario", $"Stock bajo: {nombre}",
                $"Quedan {stock}. Está en el punto de reorden o por debajo."),
            TipoAlertaInventario.QuiebreProyectado => new AvisoNuevo("AlertaInventario", $"Se acabará pronto: {nombre}",
                $"Quedan {stock}. Con el consumo proyectado bajará del punto de reorden"
                + (e.FechaQuiebreEstimada is { } fecha ? $" el {Fecha(fecha)}." : ".")),
            TipoAlertaInventario.Vencimiento => new AvisoNuevo("AlertaInventario", $"Lote por vencer: {nombre}",
                $"Un lote vence pronto. Stock actual: {stock}."),
            _ => new AvisoNuevo("AlertaInventario", $"Diferencia de stock: {nombre}",
                $"Una salida por receta pedía más de lo que había. Stock actual: {stock}.")
        };
    }, ct);

    /// <summary>Turno asignado: avisa al empleado, si tiene cuenta.</summary>
    public Task Handle(TurnoAsignadoEvent e, CancellationToken ct) => AvisarEmpleadoAsync(e.EmpleadoId,
        new AvisoNuevo("TurnoAsignado", "Turno asignado",
            $"Tienes turno el {Fecha(e.Fecha)} de {e.HoraInicio:HH\\:mm} a {e.HoraFin:HH\\:mm}."), ct);

    /// <summary>Turno modificado o cancelado: avisa al empleado, si tiene cuenta.</summary>
    public Task Handle(TurnoModificadoEvent e, CancellationToken ct) => AvisarEmpleadoAsync(e.EmpleadoId,
        new AvisoNuevo("TurnoModificado", "Tu turno cambió", $"{e.Cambio}. Revisa «Mis turnos»."), ct);

    /// <summary>Sugerencia de personal: avisa a Gerente, Supervisor y Administrador.</summary>
    public Task Handle(SugerenciaTurnoGeneradaEvent e, CancellationToken ct) => AvisarRolesAsync(GerenciaYSupervision, async consulta =>
    {
        var franja = await consulta.NombreFranjaAsync(e.FranjaHorariaId, ct) ?? "una franja";
        var cargo = await consulta.NombreCargoAsync(e.CargoId, ct) ?? "un cargo";
        var cuantas = Math.Abs(e.Diferencia);
        var personas = cuantas == 1 ? "persona" : "personas";
        var texto = e.Diferencia > 0 ? $"faltan {cuantas} {personas}" : $"sobran {cuantas} {personas}";
        return new AvisoNuevo("SugerenciaTurno", $"Sugerencia de personal: {cargo}",
            $"{franja} del {Fecha(e.Fecha)}: {texto} de {cargo}. Revisa «Sugerencias».");
    }, ct);

    /// <summary>Recomendación de Analítica: avisa a Gerente y Administrador.</summary>
    public Task Handle(RecomendacionGeneradaEvent e, CancellationToken ct) => AvisarRolesAsync(Gerencia,
        _ => Task.FromResult(new AvisoNuevo("Recomendacion", $"Recomendación ({e.Prioridad})", e.Mensaje)), ct);

    /// <summary>Fallo de entrenamiento del modelo: avisa a Gerente y Administrador. El error técnico queda solo en el log.</summary>
    public Task Handle(EntrenamientoModeloFallidoEvent e, CancellationToken ct) => AvisarRolesAsync(Gerencia,
        _ => Task.FromResult(new AvisoNuevo("EntrenamientoFallido", "Falló el entrenamiento del modelo",
            "El modelo anterior sigue activo. Revisa la pantalla Modelos.")), ct);

    private async Task AvisarRolesAsync(string[] roles, Func<IConsultaNotificaciones, Task<AvisoNuevo>> armar, CancellationToken ct)
    {
        // Scope propio: un fallo aquí no deja cambios a medias en el contexto del módulo que publicó.
        using var scope = _scopes.CreateScope();
        var consulta = scope.ServiceProvider.GetRequiredService<IConsultaNotificaciones>();
        var destinatarios = await consulta.UsuariosConRolesAsync(roles, ct);
        var aviso = await armar(consulta);
        await scope.ServiceProvider.GetRequiredService<INotificacionService>().NotificarAsync(destinatarios, aviso, ct);
    }

    private async Task AvisarEmpleadoAsync(Guid empleadoId, AvisoNuevo aviso, CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var usuarioId = await scope.ServiceProvider.GetRequiredService<IConsultaNotificaciones>().UsuarioDelEmpleadoAsync(empleadoId, ct);
        if (usuarioId is null)
        {
            _logger.LogInformation("El empleado {EmpleadoId} no tiene una cuenta activa; el aviso {Tipo} no se envió.", empleadoId, aviso.Tipo);
            return;
        }
        await scope.ServiceProvider.GetRequiredService<INotificacionService>()
            .NotificarAsync([new DestinatarioNotificacion(usuarioId.Value, MotivoEmpleado)], aviso, ct);
    }

    private static string Numero(decimal valor) => valor.ToString("0.###", CultureInfo.InvariantCulture);

    private static string Fecha(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);
}
