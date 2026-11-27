using GIRA.Application.DTOs.Configuracion;
using GIRA.Application.Interfaces.Services.Configuracion;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace GIRA.Api.Common.Configuracion;

/// <summary>
/// Canal SignalR de notificaciones (SRS 5.6, <c>/hubs/notificaciones</c>, RF-CFG-06). Solo empuja avisos al cliente:
/// leer y marcar como leída se hace por la API REST.
/// </summary>
[Authorize]
public class NotificacionesHub : Hub
{
    /// <summary>Ruta del hub.</summary>
    public const string Ruta = "/hubs/notificaciones";

    /// <summary>Nombre del mensaje que recibe el cliente.</summary>
    public const string MetodoCliente = "notificacion";

    /// <summary>Grupo de las conexiones de un usuario (puede tener varias pestañas abiertas).</summary>
    public static string Grupo(Guid usuarioId) => $"usuario:{usuarioId}";

    /// <summary>Al conectarse, la conexión entra al grupo de su usuario.</summary>
    public override async Task OnConnectedAsync()
    {
        var usuarioId = Context.User?.ObtenerUsuarioId();
        if (usuarioId is null)
        {
            Context.Abort();
            return;
        }
        await Groups.AddToGroupAsync(Context.ConnectionId, Grupo(usuarioId.Value));
        await base.OnConnectedAsync();
    }
}

/// <summary>Implementación de <see cref="IEmisorNotificaciones"/> con el hub de SignalR.</summary>
public class EmisorNotificacionesSignalR : IEmisorNotificaciones
{
    private readonly IHubContext<NotificacionesHub> _hub;

    /// <summary>Crea el emisor.</summary>
    public EmisorNotificacionesSignalR(IHubContext<NotificacionesHub> hub)
    {
        _hub = hub;
    }

    /// <inheritdoc />
    public Task EnviarAsync(Guid usuarioId, NotificacionDto notificacion, CancellationToken cancellationToken) =>
        _hub.Clients.Group(NotificacionesHub.Grupo(usuarioId)).SendAsync(NotificacionesHub.MetodoCliente, notificacion, cancellationToken);
}

/// <summary>Registro de SignalR para las notificaciones.</summary>
public static class NotificacionesExtensions
{
    /// <summary>
    /// Registra SignalR y el emisor. El navegador no puede mandar la cabecera Authorization en un WebSocket, así que el
    /// token llega como <c>access_token</c> en la URL, y solo se acepta en la ruta del hub.
    /// </summary>
    public static IServiceCollection AddNotificacionesTiempoReal(this IServiceCollection services)
    {
        services.AddSignalR();
        services.AddScoped<IEmisorNotificaciones, EmisorNotificacionesSignalR>();
        services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
        {
            var anterior = options.Events?.OnMessageReceived;
            options.Events ??= new JwtBearerEvents();
            options.Events.OnMessageReceived = async contexto =>
            {
                if (anterior is not null)
                {
                    await anterior(contexto);
                }
                var token = contexto.Request.Query["access_token"];
                if (string.IsNullOrEmpty(contexto.Token) && !string.IsNullOrEmpty(token)
                    && contexto.HttpContext.Request.Path.StartsWithSegments(NotificacionesHub.Ruta))
                {
                    contexto.Token = token;
                }
            };
        });
        return services;
    }
}
