using GIRA.Application.Common;
using GIRA.Application.DTOs.Configuracion;
using GIRA.Application.Interfaces.Repository.Configuracion;
using GIRA.Application.Interfaces.Services.Configuracion;
using GIRA.Domain.Entities.Configuracion;
using GIRA.Domain.Enums.Configuracion;
using Microsoft.Extensions.Logging;

namespace GIRA.Application.Services.Configuracion;

/// <summary>Guarda, entrega y lista las notificaciones in-app (RF-CFG-05, RF-CFG-06, RN-CFG-05).</summary>
public class NotificacionService : INotificacionService
{
    /// <summary>Cuántas notificaciones trae la bandeja.</summary>
    public const int TamanoBandeja = 50;

    private readonly INotificacionRepository _repositorio;
    private readonly IEmisorNotificaciones _emisor;
    private readonly TimeProvider _reloj;
    private readonly ILogger<NotificacionService> _logger;

    /// <summary>Crea el servicio.</summary>
    public NotificacionService(
        INotificacionRepository repositorio, IEmisorNotificaciones emisor, TimeProvider reloj, ILogger<NotificacionService> logger)
    {
        _repositorio = repositorio;
        _emisor = emisor;
        _reloj = reloj;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task NotificarAsync(
        IReadOnlyCollection<DestinatarioNotificacion> destinatarios, AvisoNuevo aviso, CancellationToken cancellationToken)
    {
        var notificaciones = destinatarios
            .DistinctBy(d => d.UsuarioId)
            .Select(d => new Notificacion
            {
                UsuarioId = d.UsuarioId,
                Destinatario = d.Motivo,
                Tipo = aviso.Tipo,
                Titulo = aviso.Titulo,
                Mensaje = aviso.Mensaje
            })
            .ToList();
        if (notificaciones.Count == 0)
        {
            _logger.LogInformation("Aviso {Tipo} sin destinatarios activos; no se guardó nada.", aviso.Tipo);
            return;
        }

        // Primero se guardan: así el aviso no se pierde aunque falle el envío en tiempo real.
        _repositorio.Agregar(notificaciones);
        await _repositorio.GuardarCambiosAsync(cancellationToken);

        foreach (var notificacion in notificaciones)
        {
            notificacion.Intentos++;
            try
            {
                await _emisor.EnviarAsync(notificacion.UsuarioId, ToDto(notificacion), cancellationToken);
                notificacion.Estado = EstadoNotificacion.Enviada;
                notificacion.FechaEnvio = _reloj.GetUtcNow().UtcDateTime;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                notificacion.Estado = EstadoNotificacion.Fallida;
                _logger.LogWarning(ex, "No se pudo enviar en tiempo real la notificación {NotificacionId}; queda en la bandeja.", notificacion.Id);
            }
        }
        await _repositorio.GuardarCambiosAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<BandejaNotificacionesDto> ListarMiasAsync(Guid usuarioId, bool? leidas, CancellationToken cancellationToken)
    {
        var notificaciones = await _repositorio.ListarDelUsuarioAsync(usuarioId, leidas, TamanoBandeja, cancellationToken);
        var noLeidas = await _repositorio.ContarNoLeidasAsync(usuarioId, cancellationToken);
        return new BandejaNotificacionesDto(notificaciones.Select(ToDto).ToList(), noLeidas);
    }

    /// <inheritdoc />
    public async Task<OperationResult> MarcarLeidaAsync(Guid id, Guid usuarioId, CancellationToken cancellationToken)
    {
        var notificacion = await _repositorio.BuscarDelUsuarioAsync(id, usuarioId, cancellationToken);
        if (notificacion is null)
        {
            return OperationResult.Failure(ErrorType.NotFound, "La notificación no existe.");
        }

        if (!notificacion.Leida)
        {
            notificacion.Leida = true;
            await _repositorio.GuardarCambiosAsync(cancellationToken);
        }
        return OperationResult.Success();
    }

    private static NotificacionDto ToDto(Notificacion n) => new(n.Id, n.Tipo, n.Titulo, n.Mensaje, n.Leida, n.CreatedAt);
}
