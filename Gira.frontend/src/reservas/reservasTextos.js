import { formatearDuracionMinutos } from './reservasFormato.js';

// Único lugar donde vive este dato: todo lo demás (reservasFormato.js, componentes)
// debe importarlo de aquí, nunca repetirlo como texto suelto.
export const TELEFONO_RESTAURANTE = '[TELÉFONO DEL RESTAURANTE]';

// Para 500 o sin red: nunca se muestra el detalle crudo del servidor.
export const MENSAJE_ERROR_GENERICO = 'No pudimos completar la acción. Revisa tu conexión e inténtalo de nuevo.';

// --- Paso 1: Fecha y franja ---

export const TEXTO_CARGANDO_DISPONIBILIDAD = 'Buscando mesas disponibles…';

export const TEXTO_SIN_DISPONIBILIDAD =
  `No hay mesas disponibles para esa fecha y esa cantidad de personas. Prueba otro día o llámanos al ${TELEFONO_RESTAURANTE}.`;

// Los textos que dependen de las políticas de reserva (GI-226) reciben las políticas: nunca llevan 30 días,
// 2 horas ni 8 personas escritos a mano.
export const textoVentanaReserva = (politicas) => {
  const dias = politicas.antelacionMaximaDias;
  return `Reservamos desde hoy hasta ${dias} ${dias === 1 ? 'día' : 'días'} adelante y con al menos ${formatearDuracionMinutos(politicas.antelacionMinimaMinutos)} de anticipación.`;
};

export const textoMasDePersonas = (politicas) => `¿Más de ${politicas.maxPersonas}? Llama al ${TELEFONO_RESTAURANTE}.`;

// Mismo texto que el servidor ("Para grupos de más de N personas, llama al restaurante.") más el teléfono.
export const textoGrupoGrande = (politicas) =>
  `Para grupos de más de ${politicas.maxPersonas} personas, llama al restaurante. ${TELEFONO_RESTAURANTE}`;

// Regla de cancelación que el diseño repite en Confirmación y en Mi reserva · Detalle.
export const textoLimiteCancelacion = (politicas) =>
  politicas.limiteCancelacionMinutos === 0
    ? 'Puedes cancelar hasta el inicio de la franja. Para cambiar algo, cancela y vuelve a reservar.'
    : `Cancelación gratis hasta ${formatearDuracionMinutos(politicas.limiteCancelacionMinutos)} antes de la franja. Para cambiar algo, cancela y vuelve a reservar.`;

// --- Franjas y personas (Paso 1) ---

export const BADGE_FRANJA_DISPONIBLE = 'Disponible';
export const BADGE_FRANJA_NO_DISPONIBLE = 'No disponible';
export const AVISO_DIA_CERRADO = 'El restaurante está cerrado ese día. Elige otra fecha.';

export const ETIQUETA_PERSONAS_NUMERO = 'Número de personas';
export const textoAyudaPersonas = (politicas) => `Entre 1 y ${politicas.maxPersonas} personas.`;
export const ERROR_PERSONAS_FORMATO = 'Escribe solo números, por ejemplo 4.';
export const ERROR_PERSONAS_MINIMO = 'Debe haber al menos 1 persona.';

// Aviso de éxito tras cancelar, como en Detalle.dc.html.
export const TEXTO_RESERVA_CANCELADA =
  'Tu reserva fue cancelada. La mesa quedó libre. Puedes reservar de nuevo cuando quieras.';

// --- Ayuda de campos ---

// Paso 2 (Tus datos): formato completo, como en Paso2.dc.html.
export const AYUDA_TELEFONO_FORMATO =
  '10 dígitos que empiecen con 809, 829 o 849. Sin +1. Puedes escribirlo con espacios o guiones.';

// Mi reserva (Buscar): versión corta, como en Consulta.dc.html.
export const AYUDA_TELEFONO_CORTA = 'Puedes escribirlo con guiones o espacios.';

// Mismo texto, carácter por carácter, que CrearReservaRequestValidator.cs y
// TelefonoRd.cs en el backend — para que la validación en cliente nunca diga
// algo distinto de lo que diría el servidor ante el mismo dato. Se usa cuando
// el cliente escribe 11 dígitos empezando en 1 (o pega "+1..."); nunca se
// recorta ni se le quita el 1 automáticamente, para que el cliente lo corrija
// él mismo y nunca se mande al servidor un teléfono así.
export const AYUDA_TELEFONO_CODIGO_PAIS =
  'Escribe solo los 10 dígitos de tu teléfono (809, 829 o 849), sin código de país.';

// No hay un texto de ayuda para el código en ningún .dc.html (solo el placeholder
// "12345678" y el maxLength); este texto es propio del frontend, no del diseño.
export const AYUDA_CODIGO = 'Los 8 dígitos que recibiste al reservar, sin espacios ni guiones.';

// --- Validación de campos (antes vivían sueltos en los componentes) ---

export const MENSAJE_NOMBRE_OBLIGATORIO = 'El nombre es obligatorio.';
// 150 es el mismo límite que CrearReservaRequestValidator.cs; se deja fijo en
// el texto (no interpolado) porque ese límite no cambia desde el cliente.
export const MENSAJE_NOMBRE_MUY_LARGO = 'El nombre no puede superar 150 caracteres.';
export const MENSAJE_TELEFONO_OBLIGATORIO = 'El teléfono es obligatorio.';

// --- Diálogo de cancelar (Mi reserva · Detalle) ---

export const TEXTO_DIALOGO_CANCELAR_TITULO = '¿Cancelar esta reserva?';
export const TEXTO_DIALOGO_CANCELAR_CUERPO =
  'La mesa quedará libre para otras personas y esta acción no se puede deshacer. Podrás hacer una nueva reserva cuando quieras.';

// Confirmación (desktop y móvil): se usa el mismo texto en ambos tamaños, no hay
// una variante corta aparte — la diferencia entre Confirmacion.dc.html y
// MovilConfirmacion.dc.html es solo de layout, que resuelve el CSS con media queries.
// Separado en título/cuerpo para que el componente pueda poner el título en negrita.
export const AVISO_SIN_CORREO_TITULO = 'No te enviamos correo.';
export const AVISO_SIN_CORREO_CUERPO =
  'Toma una captura o anota el código; sin él no podremos recuperar tu reserva. Para consultarla o cancelarla necesitarás el código y tu teléfono.';

// --- Mapa de estados (Mi reserva · Detalle) ---
//
// Confirmada y Cancelada usan los colores exactos de project/Detalle.dc.html.
// EnCurso, Completada y NoAsistio no tienen color propio en ningún .dc.html; en
// project/Mensajes.dc.html las tres se agrupan bajo la misma paleta "gris" neutra
// (fila "Reserva EnCurso, Completada o NoAsistio"), así que las tres reutilizan
// ese mismo gris en vez de inventar un color nuevo por estado.
export const ESTADOS_RESERVA = {
  Confirmada: {
    etiqueta: 'Confirmada',
    fondo: '#d9f3e8',
    texto: '#0b5a43',
    punto: '#20a77d',
    puedeCancelar: true,
  },
  EnCurso: {
    etiqueta: 'En curso',
    fondo: '#e9ebf2',
    texto: '#3d485b',
    punto: '#5d687c',
    puedeCancelar: false,
  },
  Completada: {
    etiqueta: 'Completada',
    fondo: '#e9ebf2',
    texto: '#3d485b',
    punto: '#5d687c',
    puedeCancelar: false,
  },
  Cancelada: {
    etiqueta: 'Cancelada',
    fondo: '#ffe3e5',
    texto: '#8a2a33',
    punto: '#d15c65',
    puedeCancelar: false,
  },
  NoAsistio: {
    etiqueta: 'No asististe',
    fondo: '#e9ebf2',
    texto: '#3d485b',
    punto: '#5d687c',
    puedeCancelar: false,
  },
};

// Si el servidor manda un Estado que no está en ESTADOS_RESERVA (por ejemplo,
// uno nuevo agregado después y no reflejado aquí todavía), el componente usa
// este neutro con el texto crudo del servidor en vez de romper la pantalla.
export const ESTADO_DESCONOCIDO = {
  fondo: '#e9ebf2',
  texto: '#3d485b',
  punto: '#5d687c',
  puedeCancelar: false,
};
