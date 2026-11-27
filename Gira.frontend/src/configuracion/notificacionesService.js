import { HubConnectionBuilder, LogLevel } from '@microsoft/signalr';
import { api, tokenStore } from '../security/authService.js';

const baseURL = import.meta.env?.VITE_API_BASE_URL || '';

export const notificacionesService = {
  bandeja: () => api.get('/api/notificaciones/mias').then((r) => r.data),
  marcarLeida: (id) => api.patch(`/api/notificaciones/${id}/leida`),

  /** Abre el canal en tiempo real; `alRecibir` recibe cada notificación nueva. Devuelve la conexión para cerrarla. */
  conectar(alRecibir) {
    const conexion = new HubConnectionBuilder()
      // El navegador no manda la cabecera Authorization en un WebSocket: SignalR pone el token en la URL.
      .withUrl(`${baseURL.replace(/\/$/, '')}/hubs/notificaciones`, { accessTokenFactory: () => tokenStore.getAccess() ?? '' })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();
    conexion.on('notificacion', alRecibir);
    conexion.start().catch(() => {}); // Sin tiempo real la bandeja igual se recarga al volver a la pestaña.
    return conexion;
  },
};
