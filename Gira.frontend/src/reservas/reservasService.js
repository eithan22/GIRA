import axios from 'axios';

const baseURL = import.meta.env.VITE_API_BASE_URL || '/';

// Instancia propia: nunca lleva el interceptor de Authorization de authService.
// Las pantallas públicas de Reservas no tienen sesión ni deben adjuntar el token
// de un miembro del personal que esté logueado en el mismo navegador.
const api = axios.create({
  baseURL,
  timeout: 15000,
  headers: { 'Content-Type': 'application/json' },
});

const CODIGOS_CON_DETALLE_VISIBLE = [400, 404, 409, 429];

// Devuelve el `detail` del servidor en 400/404/409/429; en cualquier otro caso
// (500, error de red, timeout) devuelve el texto genérico que pase el llamador,
// sin exponer nunca el mensaje crudo del error.
export function extraerMensajeError(error, mensajePorDefecto) {
  const status = error?.response?.status;
  const detail = error?.response?.data?.detail;
  if (CODIGOS_CON_DETALLE_VISIBLE.includes(status) && typeof detail === 'string' && detail.trim()) {
    return detail;
  }
  return mensajePorDefecto;
}

// Para que Paso 1 ignore en silencio la petición de disponibilidad que canceló
// al cambiar de día o de personas, en vez de mostrarla como error.
export function esPeticionCancelada(error) {
  return axios.isCancel(error);
}

export const reservasService = {
  async consultarDisponibilidad({ fecha, personas }, { signal } = {}) {
    const response = await api.get('/api/reservas/disponibilidad', {
      params: { fecha, personas },
      signal,
    });
    return response.data;
  },

  // Nunca manda Origen: el backend lo ignora de todos modos en el camino de
  // invitado, pero tampoco se arma el campo aquí para que no quede ninguna duda.
  async crearReserva({ nombre, telefono, fecha, franjaHorariaId, personas }) {
    const response = await api.post('/api/reservas', {
      nombre, telefono, fecha, franjaHorariaId, personas,
    });
    return response.data;
  },

  async consultarPorCodigo({ codigo, telefono }) {
    const response = await api.post('/api/reservas/consultar', { codigo, telefono });
    return response.data;
  },

  // El teléfono va en el body, nunca en la URL.
  async cancelarPorCodigo(codigo, telefono) {
    await api.patch(`/api/reservas/codigo/${encodeURIComponent(codigo)}/cancelar`, { telefono });
  },
};
