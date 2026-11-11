import { ESTADOS_RESERVA } from '../reservasTextos.js';

// Textos y mapas del personal. Lo genérico (colores de estado, mensaje de error
// genérico) se reutiliza de reservasTextos.js sin modificarlo.

export const MENSAJE_SIN_PERMISO = 'Tu rol no tiene permiso para realizar esta acción.';
export const MENSAJE_SESION_EXPIRADA = 'Tu sesión terminó. Inicia sesión nuevamente.';

export const TEXTO_CARGANDO = 'Cargando…';
export const TEXTO_PROXIMAMENTE = 'Próximamente';
export const TEXTO_REINTENTAR = 'Reintentar';

// Nombres de las pantallas: se usan en el h1 y en document.title.
export const PANTALLAS = {
  agenda: {
    titulo: 'Agenda de reservas',
    eyebrow: 'RESERVAS',
    descripcion: 'Reservas del día con filtros, búsqueda por teléfono y acciones de check-in, completar y cancelar.',
  },
  mapa: {
    titulo: 'Mapa de mesas',
    eyebrow: 'RESERVAS',
    descripcion: 'Estado de cada mesa por fecha y franja: libre, reservada, ocupada o inactiva.',
  },
  visitas: {
    titulo: 'Visitas sin reserva',
    eyebrow: 'RESERVAS',
    descripcion: 'Registro de clientes que llegan sin reserva.',
  },
  configuracion: {
    titulo: 'Zonas, mesas y franjas',
    eyebrow: 'CONFIGURACIÓN DEL SALÓN',
    descripcion: 'Administración de zonas, mesas y franjas horarias (solo Administrador y Gerente).',
  },
};

// Etiquetas para el personal. Los colores salen de ESTADOS_RESERVA (Eithan);
// solo cambia el texto donde el de cliente no sirve: «No asististe» pasa a
// «No asistió» porque el personal habla del cliente en tercera persona.
export const ESTADOS_RESERVA_PERSONAL = {
  Confirmada: { ...ESTADOS_RESERVA.Confirmada, etiqueta: 'Confirmada' },
  EnCurso: { ...ESTADOS_RESERVA.EnCurso, etiqueta: 'En curso' },
  Completada: { ...ESTADOS_RESERVA.Completada, etiqueta: 'Completada' },
  Cancelada: { ...ESTADOS_RESERVA.Cancelada, etiqueta: 'Cancelada' },
  NoAsistio: { ...ESTADOS_RESERVA.NoAsistio, etiqueta: 'No asistió' },
};
