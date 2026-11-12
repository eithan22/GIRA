import {
  AYUDA_TELEFONO_CODIGO_PAIS,
  MENSAJE_NOMBRE_MUY_LARGO,
  MENSAJE_NOMBRE_OBLIGATORIO,
} from '../reservasTextos.js';
import {
  MENSAJE_BUSQUEDA_INCOMPLETA,
  MENSAJE_BUSQUEDA_INVALIDA,
  MENSAJE_TELEFONO_INVALIDO,
  TEXTO_ORIGEN_PRESENCIAL,
  TEXTO_ORIGEN_TELEFONO,
} from './reservasPersonalTextos.js';

// Funciones puras del personal (sin React, sin DOM): se prueban con `node --test`
// (reservasPersonalFormato.test.js) y no se empaquetan salvo que un componente las importe.
// Nada aquí lee ni escribe storage, URL ni consola: el teléfono solo existe como
// argumento y valor de retorno.

const ZONA_SANTO_DOMINGO = 'America/Santo_Domingo';
const PREFIJOS_VALIDOS = ['809', '829', '849'];

// Nombres EXACTOS del enum EstadoReserva del backend (ListarReservasRequestValidator
// los acepta sin distinguir mayúsculas y rechaza números y textos con espacios).
export const ESTADOS_FILTRO = ['Confirmada', 'EnCurso', 'Completada', 'Cancelada', 'NoAsistio'];

export const TAMANO_PAGINA = 20;

function partesEnSantoDomingo(fecha) {
  const formato = new Intl.DateTimeFormat('en-CA', {
    timeZone: ZONA_SANTO_DOMINGO,
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
    hourCycle: 'h23',
  });
  return Object.fromEntries(formato.formatToParts(fecha).map((parte) => [parte.type, parte.value]));
}

/** Fecha de hoy en Santo Domingo como "aaaa-mm-dd", sin pasar por new Date(string). */
export function hoyEnSantoDomingo(ahora = new Date()) {
  const { year, month, day } = partesEnSantoDomingo(ahora);
  return `${year}-${month}-${day}`;
}

/** Hora actual en Santo Domingo como "HH:mm" (para «Actualizado a las HH:mm»). */
export function horaActualSantoDomingo(ahora = new Date()) {
  const { hour, minute } = partesEnSantoDomingo(ahora);
  return `${hour}:${minute}`;
}

/** Suma (o resta) días a "aaaa-mm-dd" con aritmética UTC: sin desfases de zona ni de horario. */
export function sumarDias(fechaIso, cantidad) {
  const [anio, mes, dia] = fechaIso.split('-').map(Number);
  const fecha = new Date(Date.UTC(anio, mes - 1, dia + cantidad));
  const mm = String(fecha.getUTCMonth() + 1).padStart(2, '0');
  const dd = String(fecha.getUTCDate()).padStart(2, '0');
  return `${fecha.getUTCFullYear()}-${mm}-${dd}`;
}

/** true si es exactamente "aaaa-mm-dd" y una fecha real. */
export function esFechaIso(texto) {
  if (typeof texto !== 'string' || !/^\d{4}-\d{2}-\d{2}$/.test(texto)) return false;
  return sumarDias(texto, 0) === texto;
}

/** Deja solo los dígitos: espacios, guiones y paréntesis no viajan al servidor. */
export function soloDigitos(texto) {
  return String(texto ?? '').replace(/\D/g, '');
}

/**
 * Valida un teléfono para buscar. Mismas reglas y mismos textos que el backend
 * (ListarReservasRequestValidator): 10 dígitos, prefijo 809/829/849, sin código de país.
 * Devuelve { valido, digitos, mensaje }; `mensaje` es '' cuando es válido.
 */
export function validarTelefono(texto) {
  const digitos = soloDigitos(texto);
  if (digitos.length === 11 && digitos.startsWith('1')) {
    return { valido: false, digitos, mensaje: AYUDA_TELEFONO_CODIGO_PAIS };
  }
  if (digitos.length !== 10 || !PREFIJOS_VALIDOS.includes(digitos.slice(0, 3))) {
    return { valido: false, digitos, mensaje: MENSAJE_TELEFONO_INVALIDO };
  }
  return { valido: true, digitos, mensaje: '' };
}

// Solo dígitos ASCII (0-9), espacios, guiones y paréntesis, con un "+" opcional al inicio: lo que alguien teclea o
// pega para un código ("1234 5678") o un teléfono ("+1 (809) 555-0199"). Cualquier otra cosa (letras, puntos, dígitos
// de otros alfabetos) es una entrada inválida.
const FORMATO_BUSQUEDA = /^\s*\+?[0-9\s\-()]*$/;

/**
 * GI-401: clasifica lo escrito en el campo único «Código o teléfono». Devuelve `{ tipo, digitos, mensaje }`:
 * - 'vacia': nada escrito.
 * - 'incompleta': 1 a 7 dígitos, o 9 (aún no es código ni teléfono); `mensaje` es una pista neutra, no un error.
 * - 'codigo': exactamente 8 dígitos (con o sin espacios y guiones); `digitos` es el código.
 * - 'telefono': 10 dígitos que empiezan por 809, 829 o 849; `digitos` es el teléfono.
 * - 'invalida': caracteres no permitidos, o 10 o más dígitos que no forman un teléfono válido (11 con "1" inicial
 *   da el aviso del código de país); `mensaje` explica por qué.
 * Función pura y sin efectos: lo escrito no se guarda ni se muestra en ninguna parte.
 */
export function clasificarBusqueda(texto) {
  const crudo = String(texto ?? '');
  if (crudo.trim() === '') return { tipo: 'vacia', digitos: '', mensaje: '' };
  if (!FORMATO_BUSQUEDA.test(crudo)) return { tipo: 'invalida', digitos: '', mensaje: MENSAJE_BUSQUEDA_INVALIDA };

  const digitos = soloDigitos(crudo);
  if (digitos.length === 0) return { tipo: 'vacia', digitos: '', mensaje: '' };
  if (digitos.length === 8) return { tipo: 'codigo', digitos, mensaje: '' };
  if (digitos.length <= 7 || digitos.length === 9) {
    return { tipo: 'incompleta', digitos, mensaje: MENSAJE_BUSQUEDA_INCOMPLETA };
  }

  const telefono = validarTelefono(digitos);
  return telefono.valido
    ? { tipo: 'telefono', digitos: telefono.digitos, mensaje: '' }
    : { tipo: 'invalida', digitos, mensaje: telefono.mensaje };
}

/** "8095550199" -> "809 555 0199"; cualquier otra cosa se devuelve tal cual. */
export function formatearTelefono(telefono) {
  const digitos = soloDigitos(telefono);
  if (digitos.length !== 10) return String(telefono ?? '');
  return `${digitos.slice(0, 3)} ${digitos.slice(3, 6)} ${digitos.slice(6)}`;
}

const ETIQUETAS_ORIGEN = { Web: 'Web', Telefono: TEXTO_ORIGEN_TELEFONO, Presencial: TEXTO_ORIGEN_PRESENCIAL };

/** Etiqueta del origen para el personal: Telefono -> "Por llamada", Presencial -> "En persona" (el valor guardado no cambia). */
export function etiquetaOrigen(origen) {
  return ETIQUETAS_ORIGEN[origen] ?? String(origen ?? '');
}

/**
 * Parámetros de GET /api/reservas a partir de los filtros aplicados. Con teléfono, la fecha
 * no se manda (el backend la ignora igual). Estado: solo nombres exactos del enum.
 */
export function armarFiltrosAgenda({ fecha, estado, franjaId, telefono, page }) {
  const filtros = { page: page || 1, pageSize: TAMANO_PAGINA };
  const digitos = soloDigitos(telefono);
  if (digitos) filtros.telefono = digitos;
  else if (fecha) filtros.fecha = fecha;
  if (ESTADOS_FILTRO.includes(estado)) filtros.estado = estado;
  if (franjaId) filtros.franjaId = franjaId;
  return filtros;
}

/** Páginas totales (mínimo 1) a partir del total del servidor. */
export function paginasTotales(total, pageSize = TAMANO_PAGINA) {
  return Math.max(1, Math.ceil((total || 0) / pageSize));
}

/** El detalle trae todos los campos de una fila de la lista: se usa para actualizarla sin recargar. */
export function detalleAFila(detalle) {
  return {
    id: detalle.id,
    codigo: detalle.codigo,
    estado: detalle.estado,
    fecha: detalle.fecha,
    franjaId: detalle.franjaId,
    franjaNombre: detalle.franjaNombre,
    franjaHoraInicio: detalle.franjaHoraInicio,
    franjaHoraFin: detalle.franjaHoraFin,
    mesaNumero: detalle.mesaNumero,
    zonaNombre: detalle.zonaNombre,
    personas: detalle.personas,
    personasReales: detalle.personasReales,
    clienteNombre: detalle.clienteNombre,
    clienteTelefono: detalle.clienteTelefono,
    origen: detalle.origen,
  };
}

const TIENE_ZONA = /(?:Z|[+-]\d{2}:?\d{2})$/i;

/**
 * Instante del servidor -> "dd/mm/aaaa HH:mm" en Santo Domingo. El servidor guarda UTC; si el
 * texto llega sin "Z" ni desplazamiento (SQL Server devuelve Kind sin especificar) se toma como UTC.
 */
export function formatearFechaHoraSantoDomingo(instanteIso) {
  if (!instanteIso) return '';
  // Se recortan los decimales a milisegundos: solo 3 dígitos están garantizados en todos los navegadores.
  const base = String(instanteIso).replace(/(\.\d{3})\d+/, '$1');
  const texto = TIENE_ZONA.test(base) ? base : `${base}Z`;
  const fecha = new Date(texto);
  if (Number.isNaN(fecha.getTime())) return '';
  const { year, month, day, hour, minute } = partesEnSantoDomingo(fecha);
  return `${day}/${month}/${year} ${hour}:${minute}`;
}

/** Validación del check-in en el cliente: entero entre 1 y la capacidad de la mesa. */
export function validarPersonasReales(texto, capacidad) {
  const limpio = String(texto ?? '').trim();
  if (!/^\d+$/.test(limpio)) {
    return { valido: false, valor: null, mensaje: 'Escribe un número entero de personas.' };
  }
  const valor = Number(limpio);
  if (valor < 1) return { valido: false, valor, mensaje: 'Debe haber al menos 1 persona.' };
  if (capacidad && valor > capacidad) {
    return { valido: false, valor, mensaje: `La mesa admite como máximo ${capacidad} personas.` };
  }
  return { valido: true, valor, mensaje: '' };
}

export const MAXIMO_MOTIVO = 500;

/** Motivo de cancelación: obligatorio y de 1 a 500 caracteres ya recortado. */
export function validarMotivo(texto) {
  const limpio = String(texto ?? '').trim();
  if (limpio.length === 0) return { valido: false, motivo: '', mensaje: 'El motivo de la cancelación es obligatorio.' };
  if (limpio.length > MAXIMO_MOTIVO) {
    return { valido: false, motivo: limpio, mensaje: `El motivo no puede superar ${MAXIMO_MOTIVO} caracteres.` };
  }
  return { valido: true, motivo: limpio, mensaje: '' };
}

// --- Nueva reserva y modificar (Lote 3, GI-395) ---

/**
 * ÚNICO lugar donde el origen se convierte al número que espera la API (OrigenReserva del backend:
 * Web = 0, Telefono = 1, Presencial = 2; el JSON no acepta nombres). Web no está aquí a propósito:
 * el personal nunca la envía y el servidor la rechazaría.
 */
export const ORIGEN_API = Object.freeze({ Telefono: 1, Presencial: 2 });

/** "Telefono" -> 1, "Presencial" -> 2. Cualquier otro valor (incluido "Web") lanza un error. */
export function origenAApi(origen) {
  if (typeof origen !== 'string' || !Object.hasOwn(ORIGEN_API, origen)) {
    throw new Error('El origen debe ser Telefono o Presencial.');
  }
  return ORIGEN_API[origen];
}

export const MAXIMO_NOMBRE = 150;
export const PERSONAS_POR_DEFECTO = '2';
export const DIAS_VENTANA = 30;

/** Nombre del cliente: obligatorio y de hasta 150 caracteres ya recortado (igual que el backend). */
export function validarNombreCliente(texto) {
  const limpio = String(texto ?? '').trim();
  if (!limpio) return { valido: false, nombre: '', mensaje: MENSAJE_NOMBRE_OBLIGATORIO };
  if (limpio.length > MAXIMO_NOMBRE) return { valido: false, nombre: limpio, mensaje: MENSAJE_NOMBRE_MUY_LARGO };
  return { valido: true, nombre: limpio, mensaje: '' };
}

/** Personas: entero de 1 en adelante. Sin tope en el cliente: el tope lo dice el servidor. */
export function validarPersonas(texto) {
  const limpio = String(texto ?? '').trim();
  if (!/^\d+$/.test(limpio)) return { valido: false, valor: null, mensaje: 'Escribe un número entero de personas.' };
  const valor = Number(limpio);
  if (valor < 1) return { valido: false, valor, mensaje: 'Debe haber al menos 1 persona.' };
  return { valido: true, valor, mensaje: '' };
}

/** Ventana de reservas: desde hoy hasta hoy + 30 días (el servidor tiene la última palabra). */
export function rangoFechasReserva(hoy) {
  return { min: hoy, max: sumarDias(hoy, DIAS_VENTANA) };
}

export function fechaEnRango(fecha, hoy) {
  const { min, max } = rangoFechasReserva(hoy);
  return esFechaIso(fecha) && fecha >= min && fecha <= max;
}

/**
 * Franjas activas con su disponibilidad: `disponible` es true/false si hay lista de disponibles, o
 * null si no se pudo confirmar (por ejemplo, un 429). Con null la franja sigue siendo elegible.
 */
export function franjasConEstado(franjas, disponibles) {
  const activas = (franjas ?? []).filter((franja) => franja.activa);
  if (!Array.isArray(disponibles)) return activas.map((franja) => ({ ...franja, disponible: null }));
  const ids = new Set(disponibles.map((franja) => franja.id));
  return activas.map((franja) => ({ ...franja, disponible: ids.has(franja.id) }));
}

/**
 * Mesas que se pueden ofrecer al modificar, a partir del mapa de ocupación: las Libres con capacidad
 * suficiente, más la mesa actual de esta reserva (Reservada con su reservaId) si también alcanza.
 */
export function mesasSeleccionables(mapa, personas, reservaId = null) {
  const resultado = [];
  for (const zona of mapa?.zonas ?? []) {
    for (const mesa of zona.mesas ?? []) {
      if (mesa.capacidad < personas) continue;
      const libre = mesa.estado === 'Libre';
      const actual = reservaId !== null && mesa.reservaId === reservaId && mesa.estado === 'Reservada';
      if (libre || actual) {
        resultado.push({
          mesaId: mesa.mesaId, numero: mesa.numero, capacidad: mesa.capacidad, zonaNombre: zona.nombre, actual,
        });
      }
    }
  }
  return resultado.sort((a, b) => a.zonaNombre.localeCompare(b.zonaNombre, 'es') || a.numero - b.numero);
}

/**
 * Filas antes/después para confirmar una modificación. La mesa solo "cambia" si el usuario eligió
 * una distinta de la actual; en automático el servidor decide y la fila lo dice con su texto.
 */
export function resumenCambios(antes, despues) {
  return [
    { clave: 'fecha', etiqueta: 'Fecha', antes: antes.fechaTexto, despues: despues.fechaTexto, cambia: antes.fecha !== despues.fecha },
    { clave: 'franja', etiqueta: 'Franja', antes: antes.franjaTexto, despues: despues.franjaTexto, cambia: antes.franjaId !== despues.franjaId },
    {
      clave: 'personas', etiqueta: 'Personas', antes: String(antes.personas), despues: String(despues.personas),
      cambia: Number(antes.personas) !== Number(despues.personas),
    },
    {
      clave: 'mesa', etiqueta: 'Mesa', antes: antes.mesaTexto, despues: despues.mesaTexto,
      cambia: Boolean(despues.mesaId) && despues.mesaId !== antes.mesaId,
    },
  ];
}

export function hayCambios(filas) {
  return filas.some((fila) => fila.cambia);
}

/** Cuerpo del PUT /api/reservas/{id}: mesaId vacío viaja como null (el servidor conserva o reasigna). */
export function armarModificacion({ fecha, franjaId, personas, mesaId }) {
  return { fecha, franjaHorariaId: franjaId, personas: Number(personas), mesaId: mesaId || null };
}

/**
 * Cuerpo del POST /api/reservas del personal. El origen sale de ORIGEN_API (número, nunca Web) y el
 * teléfono viaja solo con dígitos, en el cuerpo y nunca en la URL.
 */
export function armarCuerpoCrearReserva({ nombre, telefono, fecha, franjaHorariaId, personas, origen }) {
  return {
    nombre: String(nombre ?? '').trim(),
    telefono: soloDigitos(telefono),
    fecha,
    franjaHorariaId,
    personas: Number(personas),
    origen: origenAApi(origen),
  };
}

/** true si el formulario de nueva reserva tiene algo escrito o elegido distinto de sus valores iniciales. */
export function esBorradorCrear(formulario, hoy, cliente = null) {
  return Boolean(
    formulario.origen
    || formulario.nombre.trim()
    || soloDigitos(formulario.telefono)
    || formulario.franjaId
    || formulario.personas !== PERSONAS_POR_DEFECTO
    || formulario.fecha !== hoy
    || cliente,
  );
}

// --- Mapa de mesas y visitas sin reserva (Lote 4, GI-396) ---
// "Hoy", "franja actual" y "ya empezó" se calculan SIEMPRE en America/Santo_Domingo con Intl
// (partesEnSantoDomingo), nunca con la zona del navegador. El servidor decide en cada caso.

/** Fecha y hora actuales en Santo Domingo: { hoy: 'aaaa-mm-dd', hora: 'HH:mm' }. */
export function contextoSantoDomingo(ahora = new Date()) {
  return { hoy: hoyEnSantoDomingo(ahora), hora: horaActualSantoDomingo(ahora) };
}

/** "HH:mm" o "HH:mm:ss" -> minutos desde las 00:00. */
export function minutosDeHora(hora) {
  const [horas, minutos] = String(hora ?? '').split(':').map(Number);
  return (horas || 0) * 60 + (minutos || 0);
}

/**
 * Estado de una franja respecto a una hora ("HH:mm"): 'enCurso' (inicio <= hora < fin),
 * 'terminada' (hora >= fin) o 'noEmpezo' (hora < inicio).
 */
export function estadoFranja(franja, hora) {
  const ahora = minutosDeHora(hora);
  if (ahora < minutosDeHora(franja.horaInicio)) return 'noEmpezo';
  if (ahora >= minutosDeHora(franja.horaFin)) return 'terminada';
  return 'enCurso';
}

// Replica ParametrosReservaService.AntelacionMinima del backend (hoy fija en 2 h, provisional hasta que
// Configuración la entregue, GI-226). Solo sirve para el aviso informativo de la pantalla: quien decide
// si se puede reservar es siempre el servidor.
const MINUTOS_ANTELACION_WEB = 120;

/**
 * GI-403: true si la franja de HOY solo la puede reservar el personal: ya empezó o empieza en menos de
 * MINUTOS_ANTELACION_WEB (el cliente web ya no puede) y todavía no terminó. Informativa; el servidor decide.
 * `fecha` y `hoy` son "aaaa-mm-dd", `hora` es "HH:mm" en Santo Domingo.
 */
export function esFranjaSoloPersonal({ fecha, hoy, hora, franja }) {
  if (!franja || fecha !== hoy) return false;
  if (estadoFranja(franja, hora) === 'terminada') return false;
  return minutosDeHora(franja.horaInicio) < minutosDeHora(hora) + MINUTOS_ANTELACION_WEB;
}

function activasPorInicio(franjas) {
  return (franjas ?? [])
    .filter((franja) => franja.activa)
    .sort((a, b) => minutosDeHora(a.horaInicio) - minutosDeHora(b.horaInicio));
}

/**
 * Franja que el mapa muestra al abrirse. Si la fecha es hoy: la activa que contiene la hora actual;
 * si no hay una en curso, la próxima que empieza hoy; si ya pasaron todas, la última del día.
 * Si la fecha es otra: la primera franja activa. Nunca elige una inactiva. Sin activas, null.
 */
export function franjaPorDefecto(franjas, { fecha, hoy, hora }) {
  const activas = activasPorInicio(franjas);
  if (activas.length === 0) return null;
  if (fecha !== hoy) return activas[0];
  const enCurso = activas.find((franja) => estadoFranja(franja, hora) === 'enCurso');
  if (enCurso) return enCurso;
  const proxima = activas.find((franja) => estadoFranja(franja, hora) === 'noEmpezo');
  if (proxima) return proxima;
  return activas[activas.length - 1];
}

/** Hora de inicio ("HH:mm:ss") de la primera franja activa, o null. */
export function inicioPrimeraFranja(franjas) {
  return activasPorInicio(franjas)[0]?.horaInicio ?? null;
}

/**
 * Franjas activas con su estado hoy para registrar una visita. Solo las que no empezaron son
 * `elegible: false` (el servidor exige que la franja haya empezado, pero no que siga vigente).
 */
export function franjasParaVisita(franjas, hora) {
  return activasPorInicio(franjas).map((franja) => {
    const estado = estadoFranja(franja, hora);
    return { ...franja, estado, elegible: estado !== 'noEmpezo' };
  });
}

/** Franja preseleccionada para una visita: la actual o, si no hay, la última ya empezada. */
export function franjaDefectoVisita(franjasConEstado) {
  const elegibles = franjasConEstado.filter((franja) => franja.elegible);
  return elegibles.find((franja) => franja.estado === 'enCurso') ?? elegibles[elegibles.length - 1] ?? null;
}

/** El botón «Registrar visita» solo aplica a hoy y a una franja activa que ya empezó. */
export function puedeRegistrarVisita({ fecha, hoy, franja, hora }) {
  return Boolean(franja) && fecha === hoy && franja.activa === true && estadoFranja(franja, hora) !== 'noEmpezo';
}

export const MAXIMO_PERSONAS_SIN_MESA = 50;

/** Personas de una visita: entero ≥ 1; sin mesa hasta 50 y con mesa hasta su capacidad. */
export function validarPersonasVisita(texto, capacidadMesa = null) {
  const limpio = String(texto ?? '').trim();
  if (!/^\d+$/.test(limpio)) return { valido: false, valor: null, mensaje: 'Escribe un número entero de personas.' };
  const valor = Number(limpio);
  if (valor < 1) return { valido: false, valor, mensaje: 'Debe haber al menos 1 persona.' };
  if (capacidadMesa) {
    if (valor > capacidadMesa) return { valido: false, valor, mensaje: `La mesa admite como máximo ${capacidadMesa} personas.` };
  } else if (valor > MAXIMO_PERSONAS_SIN_MESA) {
    return { valido: false, valor, mensaje: `Sin mesa, el máximo es ${MAXIMO_PERSONAS_SIN_MESA} personas.` };
  }
  return { valido: true, valor, mensaje: '' };
}

/** Cuerpo del POST /api/visitas: sin teléfono ni datos personales; mesa vacía viaja como null. */
export function armarCuerpoVisita({ franjaId, mesaId, personas }) {
  return { franjaHorariaId: franjaId, mesaId: mesaId || null, personas: Number(personas) };
}

/** Conteo de estados de las mesas de una zona. */
export function resumenPorZona(zona) {
  const resumen = { libres: 0, reservadas: 0, ocupadas: 0, inactivas: 0, total: 0 };
  for (const mesa of zona.mesas ?? []) {
    resumen.total += 1;
    if (mesa.estado === 'Libre') resumen.libres += 1;
    else if (mesa.estado === 'Reservada') resumen.reservadas += 1;
    else if (mesa.estado === 'Ocupada') resumen.ocupadas += 1;
    else resumen.inactivas += 1;
  }
  return resumen;
}

/** Zonas del mapa ordenadas por nombre (y mesas por número) con su resumen: lista para dibujar. */
export function agruparMesasPorZona(mapa) {
  return [...(mapa?.zonas ?? [])]
    .sort((a, b) => a.nombre.localeCompare(b.nombre, 'es'))
    .map((zona) => {
      const mesas = [...(zona.mesas ?? [])].sort((a, b) => a.numero - b.numero);
      return { ...zona, mesas, resumen: resumenPorZona({ mesas }) };
    });
}

/** Suma de los resúmenes de todas las zonas (sirve para contrastar con el resumen del servidor). */
export function resumenGlobal(zonas) {
  return zonas.reduce(
    (total, zona) => {
      const r = resumenPorZona(zona);
      return {
        libres: total.libres + r.libres,
        reservadas: total.reservadas + r.reservadas,
        ocupadas: total.ocupadas + r.ocupadas,
        inactivas: total.inactivas + r.inactivas,
      };
    },
    { libres: 0, reservadas: 0, ocupadas: 0, inactivas: 0 },
  );
}

/** Mesa inactiva (ella o su zona) que todavía tiene una reserva vigente: conflicto a mostrar. */
export function esConflictoMesaInactiva(mesa) {
  return mesa.estado === 'Inactiva' && Boolean(mesa.reservaId);
}

/**
 * Acción de una mesa del mapa: 'abrirReserva' si tiene reserva (Reservada, Ocupada o Inactiva con
 * reserva), 'registrarVisita' si está Libre y se pueden registrar visitas, o null (solo informativa).
 */
export function accionMesa(mesa, { puedeVisita }) {
  if (mesa.reservaId && ['Reservada', 'Ocupada', 'Inactiva'].includes(mesa.estado)) return 'abrirReserva';
  if (mesa.estado === 'Libre' && puedeVisita) return 'registrarVisita';
  return null;
}

const ESTADO_RESERVA_DE_MESA = { Reservada: 'Confirmada', Ocupada: 'EnCurso' };

/**
 * Fila parcial para abrir PanelDetalleReserva desde una mesa: solo con lo que trae el mapa. Faltan
 * el código, el teléfono y el origen (el panel los completa al cargar el detalle) y, en una mesa
 * inactiva, el estado exacto.
 */
export function filaDesdeMesa(mapa, zona, mesa) {
  return {
    id: mesa.reservaId,
    codigo: '',
    estado: ESTADO_RESERVA_DE_MESA[mesa.estado] ?? '',
    fecha: mapa.fecha,
    franjaId: mapa.franjaId,
    franjaNombre: mapa.franjaNombre,
    franjaHoraInicio: mapa.horaInicio,
    franjaHoraFin: mapa.horaFin,
    mesaNumero: mesa.numero,
    zonaNombre: zona.nombre,
    personas: mesa.personas,
    personasReales: mesa.personasReales ?? null,
    clienteNombre: mesa.clienteNombre ?? '',
    clienteTelefono: '',
    origen: '',
  };
}

// --- Administración de zonas, mesas y franjas (Lote 5, GI-397) ---
// En el cliente solo se repiten las reglas de los validadores del backend (con sus mismos textos).
// Lo que no se puede conocer aquí (nombre o número repetido, solapamiento, reservas futuras) lo
// decide el servidor y su 409 se muestra tal cual.

export const MAXIMO_NOMBRE_ZONA = 100;
export const MAXIMO_NOMBRE_FRANJA = 50;
export const CAPACIDAD_MAXIMA_MESA = 50;
export const TAMANO_PAGINA_SALON = 20;
export const TAMANO_LISTA_SELECTORES = 100;

function validarTextoNombre(texto, { vacio, maximo, largo }) {
  const limpio = String(texto ?? '').trim();
  if (!limpio) return { valido: false, valor: '', mensaje: vacio };
  if (limpio.length > maximo) return { valido: false, valor: limpio, mensaje: largo };
  return { valido: true, valor: limpio, mensaje: '' };
}

export function validarNombreZona(texto) {
  return validarTextoNombre(texto, {
    vacio: 'El nombre de la zona es obligatorio.',
    maximo: MAXIMO_NOMBRE_ZONA,
    largo: `El nombre de la zona no puede superar los ${MAXIMO_NOMBRE_ZONA} caracteres.`,
  });
}

export function validarNombreFranja(texto) {
  return validarTextoNombre(texto, {
    vacio: 'El nombre de la franja es obligatorio.',
    maximo: MAXIMO_NOMBRE_FRANJA,
    largo: `El nombre de la franja no puede superar los ${MAXIMO_NOMBRE_FRANJA} caracteres.`,
  });
}

/** Número de mesa: entero de 1 en adelante (la unicidad global la valida el servidor). */
export function validarNumeroMesa(texto) {
  const limpio = String(texto ?? '').trim();
  const mensaje = 'El número de la mesa debe ser 1 o mayor.';
  if (limpio === '') return { valido: false, valor: null, mensaje };
  if (!/^-?\d+$/.test(limpio)) return { valido: false, valor: null, mensaje: 'Escribe un número entero.' };
  const valor = Number(limpio);
  return valor >= 1 ? { valido: true, valor, mensaje: '' } : { valido: false, valor, mensaje };
}

/** Capacidad de una mesa: entero de 1 a 50. */
export function validarCapacidadMesa(texto) {
  const limpio = String(texto ?? '').trim();
  const mensaje = `La capacidad debe estar entre 1 y ${CAPACIDAD_MAXIMA_MESA} personas.`;
  if (limpio === '') return { valido: false, valor: null, mensaje };
  if (!/^-?\d+$/.test(limpio)) return { valido: false, valor: null, mensaje: 'Escribe un número entero.' };
  const valor = Number(limpio);
  return valor >= 1 && valor <= CAPACIDAD_MAXIMA_MESA
    ? { valido: true, valor, mensaje: '' }
    : { valido: false, valor, mensaje };
}

export function validarZonaMesa(zonaId) {
  return zonaId ? { valido: true, mensaje: '' } : { valido: false, mensaje: 'Selecciona la zona de la mesa.' };
}

/**
 * "9:05", "09:05" o "09:05:00" -> "09:05:00" (el formato que devuelve y espera la API); si no es una
 * hora válida, cadena vacía. `<input type="time">` entrega "HH:mm", sin segundos.
 */
export function normalizarHora(valor) {
  const partes = /^(\d{1,2}):(\d{2})(?::(\d{2}))?(?:\.\d+)?$/.exec(String(valor ?? '').trim());
  if (!partes) return '';
  const horas = Number(partes[1]);
  const minutos = Number(partes[2]);
  const segundos = Number(partes[3] ?? 0);
  if (horas > 23 || minutos > 59 || segundos > 59) return '';
  const dos = (numero) => String(numero).padStart(2, '0');
  return `${dos(horas)}:${dos(minutos)}:${dos(segundos)}`;
}

/** Horario de una franja: ambas horas válidas y fin posterior al inicio (mismo texto que el backend). */
export function validarHorarioFranja(inicio, fin) {
  const horaInicio = normalizarHora(inicio);
  const horaFin = normalizarHora(fin);
  const errores = { inicio: '', fin: '' };
  if (!horaInicio) errores.inicio = 'Indica la hora de inicio.';
  if (!horaFin) errores.fin = 'Indica la hora de fin.';
  else if (horaInicio && horaFin <= horaInicio) errores.fin = 'La hora de fin debe ser posterior a la hora de inicio.';
  return { valido: !errores.inicio && !errores.fin, inicio: horaInicio, fin: horaFin, errores };
}

export function armarCuerpoZona({ nombre }) {
  return { nombre: String(nombre ?? '').trim() };
}

export function armarCuerpoMesa({ numero, capacidad, zonaId }) {
  return { numero: Number(numero), capacidad: Number(capacidad), zonaId };
}

/** Las horas viajan normalizadas a "HH:mm:ss". */
export function armarCuerpoFranja({ nombre, horaInicio, horaFin }) {
  return { nombre: String(nombre ?? '').trim(), horaInicio: normalizarHora(horaInicio), horaFin: normalizarHora(horaFin) };
}

/** Filtro de estado de las listas -> parámetro `activa` (undefined = todas). */
export function activaDeFiltro(filtro) {
  if (filtro === 'activas') return true;
  if (filtro === 'inactivas') return false;
  return undefined;
}

/** Zonas que se ofrecen al crear o editar una mesa: las activas más la actual (aunque esté inactiva). */
export function zonasParaMesa(zonas, zonaActualId = '') {
  return [...(zonas ?? [])]
    .filter((zona) => zona.activa || zona.id === zonaActualId)
    .sort((a, b) => a.nombre.localeCompare(b.nombre, 'es'))
    .map((zona) => ({ ...zona, actual: zona.id === zonaActualId }));
}

/** Una mesa activa cuya zona está inactiva (no se ofrece mientras la zona siga inactiva). */
export function mesaConZonaInactiva(mesa, zonas) {
  return Boolean(mesa.activa) && (zonas ?? []).find((zona) => zona.id === mesa.zonaId)?.activa === false;
}

/** true si la página pedida no trajo todos los elementos (total mayor que lo recibido). */
export function listaIncompleta(pagina) {
  return (pagina?.total ?? 0) > (pagina?.items?.length ?? 0);
}

/**
 * Mesas de una zona a partir de GET /api/mesas?zonaId=...: el total, y cuántas están activas solo
 * si la página trajo todas (si no, null: no se inventa un dato que no se conoce).
 */
export function contarMesasZona(pagina) {
  const total = pagina?.total ?? 0;
  const items = pagina?.items ?? [];
  return { total, activas: items.length === total ? items.filter((mesa) => mesa.activa).length : null };
}

export function textoImpactoZona({ total, activas }) {
  if (total === 0) return 'Esta zona no tiene mesas.';
  const mesas = `${total} ${total === 1 ? 'mesa' : 'mesas'}`;
  if (activas === null || activas === undefined) return `Esta zona tiene ${mesas}.`;
  return `Esta zona tiene ${mesas} (${activas} ${activas === 1 ? 'activa' : 'activas'}).`;
}

/** true si el mensaje del servidor es el 409 que pide reasignar reservas antes. */
export function esConflictoReasignar(mensaje) {
  return /Reasígnalas antes/.test(String(mensaje ?? ''));
}

/** Reemplaza por id el elemento actualizado dentro de una lista (sin recargarla). */
export function reemplazarPorId(items, actualizado) {
  return items.map((item) => (item.id === actualizado.id ? actualizado : item));
}
