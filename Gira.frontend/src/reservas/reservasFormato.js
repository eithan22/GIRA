const DIAS_SEMANA = ['Domingo', 'Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado'];
const DIAS_SEMANA_CORTO = ['Dom', 'Lun', 'Mar', 'Mié', 'Jue', 'Vie', 'Sáb'];
const MESES = [
  'enero', 'febrero', 'marzo', 'abril', 'mayo', 'junio',
  'julio', 'agosto', 'septiembre', 'octubre', 'noviembre', 'diciembre',
];
const MESES_CORTO = ['ene', 'feb', 'mar', 'abr', 'may', 'jun', 'jul', 'ago', 'sep', 'oct', 'nov', 'dic'];

// Convierte "2026-11-14" en sus 3 números sin pasar por new Date(string): el
// constructor de un solo argumento interpreta la fecha como UTC medianoche y,
// en zonas con offset negativo (como RD, UTC-4), la fecha mostrada se corre un día.
function partirFechaIso(fechaIso) {
  const [anio, mes, dia] = fechaIso.split('-').map(Number);
  return { anio, mes, dia };
}

// new Date(anio, mesIndex, dia) sí es seguro: construye la fecha en hora LOCAL,
// nunca en UTC, así que no hay desfase.
function diaDeSemana(fechaIso) {
  const { anio, mes, dia } = partirFechaIso(fechaIso);
  return new Date(anio, mes - 1, dia).getDay();
}

/** "2026-11-14" -> "Sábado 14 de noviembre" (resumen tipo ticket, sin año). */
export function formatearFechaLarga(fechaIso) {
  const { dia, mes } = partirFechaIso(fechaIso);
  const nombreDia = DIAS_SEMANA[diaDeSemana(fechaIso)];
  return `${nombreDia} ${dia} de ${MESES[mes - 1]}`;
}

/** "2026-11-14" -> "Sáb 14 nov 2026" (confirmación y detalle, con año). */
export function formatearFechaCorta(fechaIso) {
  const { anio, dia, mes } = partirFechaIso(fechaIso);
  const nombreDia = DIAS_SEMANA_CORTO[diaDeSemana(fechaIso)];
  return `${nombreDia} ${dia} ${MESES_CORTO[mes - 1]} ${anio}`;
}

/** "12:00:00" -> "12:00". */
export function formatearHora(horaIso) {
  return horaIso.slice(0, 5);
}

/** "48271936" -> "4827 1936". Solo para mostrar: copiar y enviar siempre los 8 dígitos tal cual. */
export function formatearCodigoVisual(codigo) {
  return `${codigo.slice(0, 4)} ${codigo.slice(4, 8)}`;
}

/** "Ana Pérez" -> "Ana", para el saludo de la confirmación. */
export function primerNombre(nombreCompleto) {
  return nombreCompleto.trim().split(/\s+/)[0] || nombreCompleto;
}

// getFullYear/getMonth/getDate del navegador ya son locales (no UTC), así que
// "hoy" se lee directo de un new Date() sin conversión.
function hoyLocal() {
  const ahora = new Date();
  return { anio: ahora.getFullYear(), mes: ahora.getMonth(), dia: ahora.getDate() };
}

// Se arma el string a mano: Date#toISOString() convierte a UTC y reintroduciría
// el mismo desfase de un día que se evita en partirFechaIso.
function aIso({ anio, mes, dia }) {
  const mm = String(mes + 1).padStart(2, '0');
  const dd = String(dia).padStart(2, '0');
  return `${anio}-${mm}-${dd}`;
}

// Sumar al componente "dia" y dejar que Date normalice el desborde de mes/año
// es seguro porque el constructor de 3 argumentos trabaja en hora local.
function sumarDiasLocal({ anio, mes, dia }, cantidad) {
  const fecha = new Date(anio, mes, dia + cantidad);
  return { anio: fecha.getFullYear(), mes: fecha.getMonth(), dia: fecha.getDate() };
}

/**
 * Hoy y los `cantidadSiguientes` días después (6 en total por defecto), para
 * los chips del Paso 1. Cada entrada trae su fecha ISO (para mandarla a la API
 * y para el min/max del input) y sus textos ya formateados para el chip.
 */
export function generarDiasChip(cantidadSiguientes = 5, politicas = POLITICAS_POR_DEFECTO, base = hoyLocal()) {
  // Nunca se ofrecen chips más allá de la antelación máxima que configura el restaurante.
  const cantidad = Math.min(cantidadSiguientes, politicas.antelacionMaximaDias);
  const dias = [];
  for (let i = 0; i <= cantidad; i += 1) {
    const componente = sumarDiasLocal(base, i);
    const iso = aIso(componente);
    dias.push({
      iso,
      dow: DIAS_SEMANA_CORTO[diaDeSemana(iso)].toUpperCase(),
      num: String(componente.dia),
      mes: MESES_CORTO[componente.mes],
      largo: formatearFechaLarga(iso),
    });
  }
  return dias;
}

/** min/max del <input type="date">: hoy y hoy + la antelación máxima de las políticas, en hora local. */
export function obtenerLimiteFechas(politicas = POLITICAS_POR_DEFECTO, hoy = hoyLocal()) {
  return {
    min: aIso(hoy),
    max: aIso(sumarDiasLocal(hoy, politicas.antelacionMaximaDias)),
  };
}

// --- Políticas de reserva (GI-226) ---

/**
 * Valores por defecto de las políticas mientras carga GET /api/reservas/politicas o si la petición falla. Es el
 * ÚNICO lugar del frontend donde viven. Espejo de ParametrosReservaService.ValoresPorDefecto del backend
 * (antelación mínima 2 h, máxima 30 días, límite de cancelación 2 h, grupo máximo 8): si cambian allá, cambian
 * aquí. El servidor siempre decide; esto solo evita pantallas vacías mientras llegan los valores reales.
 */
export const POLITICAS_POR_DEFECTO = Object.freeze({
  antelacionMinimaMinutos: 120,
  antelacionMaximaDias: 30,
  limiteCancelacionMinutos: 120,
  maxPersonas: 8,
  diasCerrados: Object.freeze([]),
});

function enteroDesde(valor, porDefecto, minimo) {
  return Number.isInteger(valor) && valor >= minimo ? valor : porDefecto;
}

/** Respuesta de /api/reservas/politicas -> políticas completas; lo que falte o venga mal se completa con el defecto. */
export function normalizarPoliticas(datos) {
  const d = POLITICAS_POR_DEFECTO;
  if (!datos || typeof datos !== 'object') return d;
  return {
    antelacionMinimaMinutos: enteroDesde(datos.antelacionMinimaMinutos, d.antelacionMinimaMinutos, 0),
    antelacionMaximaDias: enteroDesde(datos.antelacionMaximaDias, d.antelacionMaximaDias, 1),
    limiteCancelacionMinutos: enteroDesde(datos.limiteCancelacionMinutos, d.limiteCancelacionMinutos, 0),
    maxPersonas: enteroDesde(datos.maxPersonas, d.maxPersonas, 1),
    diasCerrados: Array.isArray(datos.diasCerrados) ? datos.diasCerrados.filter((x) => typeof x === 'string') : [],
  };
}

/** 120 -> "2 horas", 60 -> "1 hora", 90 -> "90 minutos" (mismo criterio que el backend). */
export function formatearDuracionMinutos(minutos) {
  if (minutos > 0 && minutos % 60 === 0) {
    const horas = minutos / 60;
    return horas === 1 ? '1 hora' : `${horas} horas`;
  }
  return minutos === 1 ? '1 minuto' : `${minutos} minutos`;
}

/** Hasta cuántos chips de personas se dibujan; por encima de esto se usa un campo numérico. */
export const MAXIMO_CHIPS_PERSONAS = 12;

/** Cantidad de chips de personas: el máximo efectivo, con tope de MAXIMO_CHIPS_PERSONAS. */
export function cantidadChipsPersonas(politicas) {
  return Math.min(politicas.maxPersonas, MAXIMO_CHIPS_PERSONAS);
}

/** True si el máximo efectivo es mayor que el tope de chips: se pide el número en un campo. */
export function usaCampoNumericoPersonas(politicas) {
  return politicas.maxPersonas > MAXIMO_CHIPS_PERSONAS;
}

/**
 * Valida el texto del campo numérico de personas (entero de 1 a `maxPersonas`). Devuelve
 * { valido, valor, error } con error 'formato' | 'minimo' | 'maximo' | null.
 */
export function validarPersonasCliente(texto, maxPersonas) {
  const limpio = String(texto ?? '').trim();
  if (!/^\d+$/.test(limpio)) return { valido: false, valor: null, error: 'formato' };
  const valor = Number(limpio);
  if (valor < 1) return { valido: false, valor: null, error: 'minimo' };
  if (valor > maxPersonas) return { valido: false, valor: null, error: 'maximo' };
  return { valido: true, valor, error: null };
}

/** Motivo del servidor cuando el restaurante no abre ese día. Espejo de MensajesReservas.RestauranteCerrado. */
export const MOTIVO_RESTAURANTE_CERRADO = 'El restaurante está cerrado ese día.';

const MOTIVO_GENERICO = 'No disponible para esa fecha y esa cantidad de personas.';

/**
 * Presentación de una franja que devuelve /api/reservas/disponibilidad (GI-226): el servidor manda todas las
 * franjas activas; las que no se pueden elegir traen disponible=false y su motivo. Si el servidor no manda el
 * campo, la franja se considera disponible. { seleccionable, motivo }.
 */
export function estadoFranjaCliente(franja) {
  const seleccionable = franja?.disponible !== false;
  if (seleccionable) return { seleccionable: true, motivo: null };
  return { seleccionable: false, motivo: franja.motivoNoDisponible || MOTIVO_GENERICO };
}

/** True si TODAS las franjas son no seleccionables porque el restaurante está cerrado ese día. */
export function diaCerradoEnFranjas(franjas) {
  return franjas.length > 0 && franjas.every((f) => f.disponible === false && f.motivoNoDisponible === MOTIVO_RESTAURANTE_CERRADO);
}

/** La franja elegida sigue siendo válida en la lista nueva: existe y se puede elegir. */
export function franjaSigueDisponible(franja, lista) {
  return Boolean(franja) && lista.some((f) => f.id === franja.id && estadoFranjaCliente(f).seleccionable);
}
