// Funciones puras de la planificación semanal y las sugerencias (GI-364, GI-365). Sin React ni red,
// para poder probarlas con node --test. Los enums viajan como números, igual que en personalFormato.js.

export const ESTADO_TURNO = { PROGRAMADO: 0, CONFIRMADO: 1, COMPLETADO: 2, AUSENTE: 3, CANCELADO: 4 };
export const ESTADO_SUGERENCIA = { PENDIENTE: 0, ACEPTADA: 1, RECHAZADA: 2, VENCIDA: 3 };

const ETIQUETAS_TURNO = { 0: 'Programado', 1: 'Confirmado', 2: 'Completado', 3: 'Ausente', 4: 'Cancelado' };
const ETIQUETAS_SUGERENCIA = { 0: 'Pendiente', 1: 'Aceptada', 2: 'Rechazada', 3: 'Vencida' };

export function etiquetaEstadoTurno(valor) {
  return ETIQUETAS_TURNO[valor] ?? '—';
}

export function etiquetaEstadoSugerencia(valor) {
  return ETIQUETAS_SUGERENCIA[valor] ?? '—';
}

// Las fechas van como YYYY-MM-DD y se calculan en UTC para no depender de la zona horaria del navegador.
function aFecha(iso) {
  const [anio, mes, dia] = iso.split('-').map(Number);
  return new Date(Date.UTC(anio, mes - 1, dia));
}

function aIso(fecha) {
  return fecha.toISOString().slice(0, 10);
}

/** Desplaza una fecha YYYY-MM-DD un número de días (negativo para retroceder). */
export function sumarDias(iso, dias) {
  const fecha = aFecha(iso);
  fecha.setUTCDate(fecha.getUTCDate() + dias);
  return aIso(fecha);
}

/** Lunes de la semana que contiene la fecha dada. */
export function lunesDe(iso) {
  const diaSemana = (aFecha(iso).getUTCDay() + 6) % 7; // lunes = 0, domingo = 6
  return sumarDias(iso, -diaSemana);
}

/** Los siete días, de lunes a domingo, de la semana que empieza en el lunes dado. */
export function diasDeSemana(lunes) {
  return Array.from({ length: 7 }, (_, i) => sumarDias(lunes, i));
}

/** Hora de la API (HH:mm:ss) en formato corto HH:mm. */
export function horaCorta(hora) {
  return typeof hora === 'string' ? hora.slice(0, 5) : '';
}

/** Errores de asignar o modificar un turno: { campo: mensaje }. Vacío si es válido. */
export function validarTurno(form) {
  const errores = {};
  if (!form.empleadoId) errores.empleadoId = 'Elige el empleado.';
  if (!form.cargoId) errores.cargoId = 'Elige el cargo del turno.';
  if (!form.fecha) errores.fecha = 'La fecha es obligatoria.';
  if (!form.horaInicio) errores.horaInicio = 'La hora de inicio es obligatoria.';
  if (!form.horaFin) errores.horaFin = 'La hora de fin es obligatoria.';
  if (form.horaInicio && form.horaFin && form.horaInicio >= form.horaFin) {
    errores.horaFin = 'La hora de fin debe ser posterior a la de inicio.';
  }
  if ((form.notas ?? '').trim().length > 250) errores.notas = 'Las notas no pueden superar 250 caracteres.';
  return errores;
}

/** Cuerpo que espera la API para crear o modificar un turno. Las horas van como HH:mm:00. */
export function aEnvioTurno(form) {
  const notas = (form.notas ?? '').trim();
  return {
    empleadoId: form.empleadoId,
    cargoId: form.cargoId,
    fecha: form.fecha,
    horaInicio: `${form.horaInicio}:00`,
    horaFin: `${form.horaFin}:00`,
    notas: notas || null,
  };
}

/**
 * Turnos vigentes (Programado o Confirmado) de un cargo que cubren la franja completa en una fecha.
 * Es la misma regla de cobertura del backend (GI-301), usada para mostrar quién está en cada celda.
 */
export function turnosQueCubren(turnos, { fecha, cargoId, horaInicio, horaFin }) {
  return turnos.filter((t) =>
    t.fecha === fecha &&
    t.cargoId === cargoId &&
    (t.estado === ESTADO_TURNO.PROGRAMADO || t.estado === ESTADO_TURNO.CONFIRMADO) &&
    t.horaInicio <= horaInicio &&
    t.horaFin >= horaFin);
}

/** Texto de la diferencia de una sugerencia: positivo faltan personas, negativo sobran. */
export function textoDiferencia(diferencia) {
  if (diferencia > 0) return `Faltan ${diferencia}`;
  if (diferencia < 0) return `Sobran ${Math.abs(diferencia)}`;
  return 'Sin diferencia';
}

/** Solo las sugerencias pendientes se pueden aceptar o rechazar (RF-PER-10). */
export function puedeResolver(sugerencia) {
  return sugerencia.estado === ESTADO_SUGERENCIA.PENDIENTE;
}

/** Opciones del filtro de estado de sugerencias. El valor vacío significa todas. */
export const FILTRO_SUGERENCIAS = [
  { valor: '', etiqueta: 'Todas' },
  { valor: ESTADO_SUGERENCIA.PENDIENTE, etiqueta: 'Pendientes' },
  { valor: ESTADO_SUGERENCIA.ACEPTADA, etiqueta: 'Aceptadas' },
  { valor: ESTADO_SUGERENCIA.RECHAZADA, etiqueta: 'Rechazadas' },
  { valor: ESTADO_SUGERENCIA.VENCIDA, etiqueta: 'Vencidas' },
];

/** Un turno ya pasado (fecha anterior a hoy) que todavía está vigente: se puede marcar como Completado o Ausente (GI-380, RN-PER-10). */
export function puedeMarcarPasado(turno, hoyIso) {
  return (turno.estado === ESTADO_TURNO.PROGRAMADO || turno.estado === ESTADO_TURNO.CONFIRMADO) && turno.fecha < hoyIso;
}

/** Un turno vigente de hoy en adelante se puede modificar o cancelar (RN-PER-10). */
export function puedeModificarTurno(turno, hoyIso) {
  return (turno.estado === ESTADO_TURNO.PROGRAMADO || turno.estado === ESTADO_TURNO.CONFIRMADO) && turno.fecha >= hoyIso;
}
