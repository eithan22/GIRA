import { api } from '../../security/authService.js';
import { extraerMensajeError, esPeticionCancelada } from '../reservasService.js';
import { MENSAJE_ERROR_GENERICO } from '../reservasTextos.js';
import { MENSAJE_SIN_PERMISO } from './reservasPersonalTextos.js';

// Servicio del personal: usa el `api` de authService (token + refresh). El 401 ya
// lo resuelve ese interceptor (renueva o dispara 'gira:session-expired' y la app
// vuelve al login), así que aquí NO se duplica esa lógica. Un 403 nunca cierra la
// sesión: solo se muestra el mensaje.
//
// Datos personales: nunca van en la ruta del navegador, en storage ni en logs.
// EXCEPCIÓN documentada (docs/proteger-endpoints.md): listarReservas manda el
// teléfono en el query string de GET /api/reservas?telefono=, solo para personal
// autenticado. Vive únicamente en el estado de la página que lo escribió.

const TIMEOUT_MS = 15000;

export { esPeticionCancelada };

/** Devuelve el texto a mostrar: 403 con mensaje propio; el resto, como en la parte pública. */
export function mensajeDeError(error, mensajePorDefecto = MENSAJE_ERROR_GENERICO) {
  if (error?.response?.status === 403) return MENSAJE_SIN_PERMISO;
  return extraerMensajeError(error, mensajePorDefecto);
}

/** true si el error es un 401 (la sesión ya se manejó en el interceptor de authService). */
export function esSesionExpirada(error) {
  return error?.response?.status === 401;
}

function opciones(signal, extra = {}) {
  return { timeout: TIMEOUT_MS, signal, ...extra };
}

// Quita claves undefined/null/'' para no ensuciar el query string.
function limpiar(params) {
  return Object.fromEntries(
    Object.entries(params).filter(([, valor]) => valor !== undefined && valor !== null && valor !== ''),
  );
}

async function listarSalon(recurso, filtros, signal) {
  const response = await api.get(`/api/${recurso}`, opciones(signal, { params: limpiar(filtros) }));
  return response.data;
}

export const reservasPersonalService = {
  // --- Agenda y detalle (OperarReservas) ---

  /** GET /api/reservas. Sin fecha ni teléfono devuelve hoy. `telefono`: excepción documentada (ver arriba). */
  async listarReservas({ fecha, estado, franjaId, telefono, page = 1, pageSize = 20 } = {}, { signal } = {}) {
    const response = await api.get('/api/reservas', opciones(signal, {
      params: limpiar({ fecha, estado, franjaId, telefono, page, pageSize }),
    }));
    return response.data;
  },

  async obtenerReserva(id, { signal } = {}) {
    const response = await api.get(`/api/reservas/${id}`, opciones(signal));
    return response.data;
  },

  async checkIn(id, personasReales) {
    const response = await api.patch(`/api/reservas/${id}/check-in`, { personasReales }, opciones());
    return response.data;
  },

  async completar(id) {
    const response = await api.patch(`/api/reservas/${id}/completar`, null, opciones());
    return response.data;
  },

  async cancelar(id, motivo) {
    const response = await api.patch(`/api/reservas/${id}/cancelar`, { motivo }, opciones());
    return response.data;
  },

  async modificar(id, { fecha, franjaHorariaId, personas, mesaId }) {
    const response = await api.put(`/api/reservas/${id}`, {
      fecha, franjaHorariaId, personas, mesaId: mesaId ?? null,
    }, opciones());
    return response.data;
  },

  /** POST /api/reservas con token: `origen` debe ser Telefono o Presencial (verificar formato en el Lote 3). */
  async crearReserva({ nombre, telefono, fecha, franjaHorariaId, personas, origen }) {
    const response = await api.post('/api/reservas', {
      nombre, telefono, fecha, franjaHorariaId, personas, origen,
    }, opciones());
    return response.data;
  },

  // --- Visitas sin reserva ---

  async registrarVisita({ franjaHorariaId, mesaId, personas }) {
    const response = await api.post('/api/visitas', {
      franjaHorariaId, mesaId: mesaId ?? null, personas,
    }, opciones());
    return response.data;
  },

  // --- Mapa de ocupación (GI-167). También es la fuente de mesas libres para modificar y visitas. ---

  async obtenerOcupacion({ fecha, franjaId }, { signal } = {}) {
    const response = await api.get('/api/reservas/ocupacion', opciones(signal, {
      params: limpiar({ fecha, franjaId }),
    }));
    return response.data;
  },

  // --- Clientes ---

  async buscarClientes({ buscar, page = 1, pageSize = 20 }, { signal } = {}) {
    const response = await api.get('/api/clientes', opciones(signal, {
      params: limpiar({ buscar, page, pageSize }),
    }));
    return response.data;
  },

  // --- Zonas, mesas y franjas (GestionarSalon: Administrador y Gerente) ---

  listarZonas: (filtros = {}, { signal } = {}) => listarSalon('zonas', filtros, signal),
  listarMesas: (filtros = {}, { signal } = {}) => listarSalon('mesas', filtros, signal),
  listarFranjas: (filtros = {}, { signal } = {}) => listarSalon('franjas', filtros, signal),

  async guardarZona(datos, id) {
    const response = id
      ? await api.put(`/api/zonas/${id}`, { nombre: datos.nombre }, opciones())
      : await api.post('/api/zonas', { nombre: datos.nombre }, opciones());
    return response.data;
  },

  async guardarMesa(datos, id) {
    const cuerpo = { numero: datos.numero, capacidad: datos.capacidad, zonaId: datos.zonaId };
    const response = id
      ? await api.put(`/api/mesas/${id}`, cuerpo, opciones())
      : await api.post('/api/mesas', cuerpo, opciones());
    return response.data;
  },

  async guardarFranja(datos, id) {
    const cuerpo = { nombre: datos.nombre, horaInicio: datos.horaInicio, horaFin: datos.horaFin };
    const response = id
      ? await api.put(`/api/franjas/${id}`, cuerpo, opciones())
      : await api.post('/api/franjas', cuerpo, opciones());
    return response.data;
  },

  async cambiarEstadoZona(id, activa) {
    const response = await api.patch(`/api/zonas/${id}/estado`, { activa }, opciones());
    return response.data;
  },

  async cambiarEstadoMesa(id, activa) {
    const response = await api.patch(`/api/mesas/${id}/estado`, { activa }, opciones());
    return response.data;
  },

  async cambiarEstadoFranja(id, activa) {
    const response = await api.patch(`/api/franjas/${id}/estado`, { activa }, opciones());
    return response.data;
  },
};
