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
export function generarDiasChip(cantidadSiguientes = 5) {
  const base = hoyLocal();
  const dias = [];
  for (let i = 0; i <= cantidadSiguientes; i += 1) {
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

/** min/max del <input type="date">: hoy y hoy+30, calculados en hora local. */
export function obtenerLimiteFechas() {
  const hoy = hoyLocal();
  return {
    min: aIso(hoy),
    max: aIso(sumarDiasLocal(hoy, 30)),
  };
}
