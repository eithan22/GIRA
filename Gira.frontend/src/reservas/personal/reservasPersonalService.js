import { api } from '../../security/authService.js';
import { extraerMensajeError, esPeticionCancelada } from '../reservasService.js';
import { MENSAJE_ERROR_GENERICO } from '../reservasTextos.js';
import {
  armarCuerpoCrearReserva,
  armarCuerpoFranja,
  armarCuerpoMesa,
  armarCuerpoVisita,
  armarCuerpoZona,
} from './reservasPersonalFormato.js';
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

  /**
   * GET /api/reservas/codigo/{codigo} (GI-401): la reserva con ese código de 8 dígitos, de cualquier fecha y estado.
   * El código va en la ruta de la API (nunca en la del navegador ni en storage) y solo vive en el estado de la
   * pantalla. 404 si no existe; 429 si se pasa el límite por usuario (la pantalla lo trata con texto propio).
   */
  async obtenerReservaPorCodigo(codigo, { signal } = {}) {
    const response = await api.get(`/api/reservas/codigo/${encodeURIComponent(codigo)}`, opciones(signal));
    return response.data;
  },

  /** GET /api/reservas. Sin fecha ni teléfono devuelve hoy. `telefono`: excepción documentada (ver arriba). */
  async listarReservas({ fecha, estado, franjaId, telefono, page = 1, pageSize = 20 } = {}, { signal } = {}) {
    // El teléfono viaja solo con dígitos; axios lo codifica en el query string.
    const digitos = telefono ? String(telefono).replace(/\D/g, '') : undefined;
    const response = await api.get('/api/reservas', opciones(signal, {
      params: limpiar({ fecha, estado, franjaId, telefono: digitos, page, pageSize }),
    }));
    return response.data;
  },

  /** GET /api/reservas/franjas (GI-399): todas las franjas no borradas, con `activa`, por hora de inicio. */
  async listarFranjasOperativas({ signal } = {}) {
    const response = await api.get('/api/reservas/franjas', opciones(signal));
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

  async marcarNoAsistio(id) {
    const response = await api.patch(`/api/reservas/${id}/no-asistio`, null, opciones());
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

  /**
   * POST /api/reservas con token. `origen` es "Telefono" o "Presencial" y viaja como número
   * (ORIGEN_API, única fuente); el teléfono va solo en el cuerpo, con dígitos. Devuelve ReservaCreadaDto.
   */
  async crearReserva(datos) {
    const response = await api.post('/api/reservas', armarCuerpoCrearReserva(datos), opciones());
    return response.data;
  },

  /**
   * GET /api/reservas/disponibilidad-personal (GI-403): franjas con mesa libre para fecha y personas,
   * con la regla del personal: incluye la franja de hoy en curso o a menos de 2 h y solo excluye las que
   * ya terminaron. Va con token (OperarReservas) y sin límite por IP; un error no debe bloquear el formulario.
   */
  async consultarDisponibilidad({ fecha, personas }, { signal } = {}) {
    const response = await api.get('/api/reservas/disponibilidad-personal', opciones(signal, { params: { fecha, personas } }));
    return response.data;
  },

  // --- Visitas sin reserva ---

  /**
   * GET /api/visitas?fecha= (GI-402): visitas sin reserva de un día, sin datos personales. La fecha va SIEMPRE explícita
   * y como aaaa-mm-dd (el servidor rechaza cualquier otro formato). Devuelve { fecha, totalVisitas, totalPersonas,
   * truncada, visitas: [{ id, franjaNombre, mesaNumero, personas, horaRegistro }] }; `horaRegistro` puede ser null.
   */
  async listarVisitas({ fecha }, { signal } = {}) {
    const response = await api.get('/api/visitas', opciones(signal, { params: { fecha } }));
    return response.data;
  },

  /** POST /api/visitas: la fecha la pone el servidor (hoy). Sin datos personales; devuelve VisitaDto. */
  async registrarVisita(datos) {
    const response = await api.post('/api/visitas', armarCuerpoVisita(datos), opciones());
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

  // Crear (POST, sin id) o editar (PUT /{id}). Los cuerpos salen de los armadores puros (recorte,
  // tipos y horas "HH:mm:ss"). Sin RowVersion: ningún DTO lo expone, no hay nada que reenviar.
  async guardarZona(datos, id) {
    const cuerpo = armarCuerpoZona(datos);
    const response = id
      ? await api.put(`/api/zonas/${id}`, cuerpo, opciones())
      : await api.post('/api/zonas', cuerpo, opciones());
    return response.data;
  },

  async guardarMesa(datos, id) {
    const cuerpo = armarCuerpoMesa(datos);
    const response = id
      ? await api.put(`/api/mesas/${id}`, cuerpo, opciones())
      : await api.post('/api/mesas', cuerpo, opciones());
    return response.data;
  },

  async guardarFranja(datos, id) {
    const cuerpo = armarCuerpoFranja(datos);
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
