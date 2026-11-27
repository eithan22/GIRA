namespace GIRA.Application.DTOs.Configuracion;

/// <summary>Notificación tal como la ve su destinatario.</summary>
public sealed record NotificacionDto(Guid Id, string Tipo, string Titulo, string Mensaje, bool Leida, DateTime FechaCreacion);

/// <summary>Bandeja del usuario: las más recientes y cuántas no ha leído (para el contador de la campana).</summary>
public sealed record BandejaNotificacionesDto(IReadOnlyList<NotificacionDto> Notificaciones, int NoLeidas);

/// <summary>A quién avisar y por qué (el rol, o «Empleado» si es su propio turno).</summary>
public sealed record DestinatarioNotificacion(Guid UsuarioId, string Motivo);

/// <summary>Contenido de un aviso, antes de repartirlo entre los destinatarios.</summary>
public sealed record AvisoNuevo(string Tipo, string Titulo, string Mensaje);
