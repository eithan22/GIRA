// Funciones puras de Personal y Turnos: validación de formularios, forma del envío y mensajes de error.
// Sin React ni red, para poder probarlas con node --test.

const PATRON_CORREO = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

function texto(valor) {
  return typeof valor === 'string' ? valor.trim() : '';
}

/** Errores de un cargo: { campo: mensaje }. Vacío si es válido. */
export function validarCargo({ nombre, descripcion }) {
  const errores = {};
  const nombreLimpio = texto(nombre);
  if (!nombreLimpio) errores.nombre = 'El nombre del cargo es obligatorio.';
  else if (nombreLimpio.length > 100) errores.nombre = 'El nombre no puede superar 100 caracteres.';
  if (texto(descripcion).length > 250) errores.descripcion = 'La descripción no puede superar 250 caracteres.';
  return errores;
}

/** Errores de un empleado: { campo: mensaje }. Vacío si es válido. */
export function validarEmpleado(form) {
  const errores = {};
  if (!texto(form.nombre)) errores.nombre = 'El nombre es obligatorio.';
  if (!texto(form.apellido)) errores.apellido = 'El apellido es obligatorio.';
  if (!texto(form.cedula)) errores.cedula = 'La cédula es obligatoria.';
  else if (texto(form.cedula).length > 20) errores.cedula = 'La cédula no puede superar 20 caracteres.';
  if (texto(form.telefono).length > 20) errores.telefono = 'El teléfono no puede superar 20 caracteres.';
  if (texto(form.email) && !PATRON_CORREO.test(texto(form.email))) errores.email = 'El correo no tiene un formato válido.';
  if (!form.cargoId) errores.cargoId = 'Elige el cargo del empleado.';
  if (!form.fechaIngreso) errores.fechaIngreso = 'La fecha de ingreso es obligatoria.';
  const horas = Number(form.horasMaximasSemana);
  if (!Number.isInteger(horas) || horas < 1 || horas > 168) {
    errores.horasMaximasSemana = 'Las horas máximas por semana deben ser un número entre 1 y 168.';
  }
  return errores;
}

/** Cuerpo que espera la API para crear o editar un empleado. Los opcionales vacíos van como null. */
export function aEnvioEmpleado(form) {
  return {
    nombre: texto(form.nombre),
    apellido: texto(form.apellido),
    cedula: texto(form.cedula),
    telefono: texto(form.telefono) || null,
    email: texto(form.email) || null,
    cargoId: form.cargoId,
    fechaIngreso: form.fechaIngreso,
    horasMaximasSemana: Number(form.horasMaximasSemana),
  };
}

/** Cuerpo que espera la API para crear o editar un cargo. */
export function aEnvioCargo({ nombre, descripcion }) {
  return { nombre: texto(nombre), descripcion: texto(descripcion) || null };
}

/** Mensaje para mostrar al usuario: el detalle del servidor si lo hay, si no el texto por defecto. */
export function mensajeDeError(error, porDefecto) {
  const detalle = error?.response?.data?.detail;
  return typeof detalle === 'string' && detalle.trim() ? detalle : porDefecto;
}

export function etiquetaEstado(activo) {
  return activo ? 'Activo' : 'Inactivo';
}

// La API envía los enums de Personal como números (sin JsonStringEnumConverter).
export const TIPO_AUSENCIA = { VACACIONES: 0, PERMISO: 1, LICENCIA: 2 };
export const ESTADO_AUSENCIA = { SOLICITADA: 0, APROBADA: 1, RECHAZADA: 2 };

export const TIPOS_AUSENCIA_OPCIONES = [
  { valor: TIPO_AUSENCIA.VACACIONES, etiqueta: 'Vacaciones' },
  { valor: TIPO_AUSENCIA.PERMISO, etiqueta: 'Permiso' },
  { valor: TIPO_AUSENCIA.LICENCIA, etiqueta: 'Licencia' },
];

const ETIQUETAS_ESTADO_AUSENCIA = {
  [ESTADO_AUSENCIA.SOLICITADA]: 'Solicitada',
  [ESTADO_AUSENCIA.APROBADA]: 'Aprobada',
  [ESTADO_AUSENCIA.RECHAZADA]: 'Rechazada',
};

export function etiquetaEstadoAusencia(valor) {
  return ETIQUETAS_ESTADO_AUSENCIA[valor] ?? 'Desconocido';
}

/** Errores de una solicitud de ausencia: { campo: mensaje }. Vacío si es válida. */
export function validarAusencia(form) {
  const errores = {};
  if (!form.empleadoId) errores.empleadoId = 'Elige el empleado.';
  if (!form.fechaDesde) errores.fechaDesde = 'La fecha de inicio es obligatoria.';
  if (!form.fechaHasta) errores.fechaHasta = 'La fecha de fin es obligatoria.';
  if (form.fechaDesde && form.fechaHasta && form.fechaDesde > form.fechaHasta) {
    errores.fechaHasta = 'La fecha de fin no puede ser anterior a la de inicio.';
  }
  if (!TIPOS_AUSENCIA_OPCIONES.some((t) => t.valor === Number(form.tipo))) errores.tipo = 'Elige el tipo de ausencia.';
  if (texto(form.motivo).length > 250) errores.motivo = 'El motivo no puede superar 250 caracteres.';
  return errores;
}

/** Cuerpo que espera la API para registrar una ausencia. */
export function aEnvioAusencia(form) {
  return {
    empleadoId: form.empleadoId,
    fechaDesde: form.fechaDesde,
    fechaHasta: form.fechaHasta,
    tipo: Number(form.tipo),
    motivo: texto(form.motivo) || null,
  };
}

/** Errores de un requerimiento de personal: { campo: mensaje }. Vacío si es válido. */
export function validarRequerimiento({ cargoId, comensalesPorEmpleado, minimoPorFranja }) {
  const errores = {};
  if (!cargoId) errores.cargoId = 'Falta el cargo.';
  if (!Number.isInteger(Number(comensalesPorEmpleado)) || Number(comensalesPorEmpleado) < 1) {
    errores.comensalesPorEmpleado = 'Debe ser un número mayor que cero.';
  }
  if (!Number.isInteger(Number(minimoPorFranja)) || Number(minimoPorFranja) < 1) {
    errores.minimoPorFranja = 'Debe ser un número mayor que cero.';
  }
  return errores;
}

/** Cuerpo que espera la API para guardar un requerimiento. */
export function aEnvioRequerimiento({ cargoId, comensalesPorEmpleado, minimoPorFranja }) {
  return {
    cargoId,
    comensalesPorEmpleado: Number(comensalesPorEmpleado),
    minimoPorFranja: Number(minimoPorFranja),
  };
}

/** Horas decimales que devuelve la API, en texto legible: "5 min", "1 h 30 min" o "2 h". */
export function formatearHoras(horas) {
  if (horas === null || horas === undefined || Number.isNaN(Number(horas))) return '—';
  const minutos = Math.round(Number(horas) * 60);
  const h = Math.floor(minutos / 60);
  const m = minutos % 60;
  if (h === 0) return `${m} min`;
  return m === 0 ? `${h} h` : `${h} h ${m} min`;
}
