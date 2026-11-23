// Funciones puras de Mi disponibilidad y Mis turnos (GI-363). Sin React ni red, para probarlas con node --test.
// DayOfWeek viaja como número: 0 = domingo, 1 = lunes … 6 = sábado.

// Los días se muestran desde el lunes, como en la planificación.
export const DIAS_EN_ORDEN = [1, 2, 3, 4, 5, 6, 0];

const NOMBRES_DIA = ['Domingo', 'Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado'];

export function nombreDia(numero) {
  return NOMBRES_DIA[numero] ?? '—';
}

/** Ordena bloques de disponibilidad de lunes a domingo y, dentro del día, por hora de inicio. */
export function ordenarDisponibilidad(bloques) {
  const posicion = (dia) => DIAS_EN_ORDEN.indexOf(dia);
  return [...bloques].sort((a, b) =>
    posicion(a.diaSemana) - posicion(b.diaSemana) || a.horaInicio.localeCompare(b.horaInicio));
}

/** Errores de un bloque de disponibilidad: { campo: mensaje }. Vacío si es válido. */
export function validarDisponibilidad({ diaSemana, horaInicio, horaFin }) {
  const errores = {};
  if (diaSemana === '' || diaSemana === null || diaSemana === undefined) errores.diaSemana = 'Elige el día.';
  if (!horaInicio) errores.horaInicio = 'La hora de inicio es obligatoria.';
  if (!horaFin) errores.horaFin = 'La hora de fin es obligatoria.';
  if (horaInicio && horaFin && horaInicio >= horaFin) {
    errores.horaFin = 'La hora de fin debe ser posterior a la de inicio.';
  }
  return errores;
}

/** Cuerpo que espera la API: el día como número y las horas como HH:mm:00. */
export function aEnvioDisponibilidad({ diaSemana, horaInicio, horaFin }) {
  return {
    diaSemana: Number(diaSemana),
    horaInicio: `${horaInicio}:00`,
    horaFin: `${horaFin}:00`,
  };
}
