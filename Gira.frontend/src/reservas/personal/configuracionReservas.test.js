// Pruebas de funciones puras de la pantalla de Configuración de reservas (GI-226, Lote 6) y de los valores de
// antelación que lee el personal. Se corren con el ejecutor de Node, sin instalar nada:
//   node --test src/reservas/personal/configuracionReservas.test.js
// No se importa desde ningún componente, así que Vite no las empaqueta.
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { errorPorCampo, extraerMensajeError } from '../reservasErrores.js';
import { POLITICAS_POR_DEFECTO } from '../reservasFormato.js';
import {
  CAMPOS_POLITICAS,
  DIAS_HORARIO_ORDEN,
  NOMBRES_DIA,
  armarCuerpoDiaCerrado,
  armarCuerpoHorario,
  esFranjaSoloPersonal,
  fechaEnRango,
  formularioAPoliticas,
  horarioAFormulario,
  horariosIguales,
  politicasAFormulario,
  politicasIguales,
  rangoFechasReserva,
  textoLegiblePolitica,
  validarDiaCerrado,
  validarHorarioDia,
  validarPoliticas,
} from './reservasPersonalFormato.js';
import { avisoFranjaSoloPersonal, ayudaNuevoHorario, mensajeFechaFueraDeRango } from './reservasPersonalTextos.js';

const CARPETA = path.dirname(fileURLToPath(import.meta.url));

const VALIDAS = {
  antelacionMinimaMinutos: '120',
  antelacionMaximaDias: '30',
  limiteCancelacionMinutos: '120',
  ventanaCheckInMinutos: '15',
  maxReservasVigentesPorTelefono: '2',
  maxPersonasPorReserva: '8',
};

// --- Días del horario: de lunes a domingo, con el DayOfWeek de .NET ---

test('DIAS_HORARIO_ORDEN: lunes a domingo con el DayOfWeek de .NET (domingo = 0 va al final)', () => {
  assert.deepEqual([...DIAS_HORARIO_ORDEN], [1, 2, 3, 4, 5, 6, 0]);
  assert.deepEqual(DIAS_HORARIO_ORDEN.map((d) => NOMBRES_DIA[d]), [
    'Lunes', 'Martes', 'Miércoles', 'Jueves', 'Viernes', 'Sábado', 'Domingo',
  ]);
  assert.equal(new Set(DIAS_HORARIO_ORDEN).size, 7);
});

// --- Errores del servidor ---

test('errorPorCampo: convierte un ValidationProblem 400 en { campo: primer mensaje }', () => {
  const error = {
    response: {
      status: 400,
      data: { errors: { antelacionMinimaMinutos: ['Debe ser menor.', 'Otro'], maxPersonasPorReserva: ['Entre 1 y 50.'] } },
    },
  };
  assert.deepEqual(errorPorCampo(error), {
    antelacionMinimaMinutos: 'Debe ser menor.',
    maxPersonasPorReserva: 'Entre 1 y 50.',
  });
});

test('errorPorCampo: sin errors, con otro estado o sin respuesta devuelve {}', () => {
  assert.deepEqual(errorPorCampo({ response: { status: 400, data: { detail: 'x' } } }), {});
  assert.deepEqual(errorPorCampo({ response: { status: 409, data: { errors: { a: ['x'] } } } }), {});
  assert.deepEqual(errorPorCampo(new Error('red')), {});
  assert.deepEqual(errorPorCampo(undefined), {});
});

test('extraerMensajeError: el 409 de franja fuera del horario llega tal cual, con el día y el horario (no un error genérico)', () => {
  const detail = 'La franja (09:00–11:00) no cabe en el horario del lunes (12:00–22:30). Ajusta la franja o el horario de operación.';
  assert.equal(extraerMensajeError({ response: { status: 409, data: { detail } } }, 'Error genérico'), detail);
  const franjaDelHorario = 'La franja \'Cena\' (18:30–20:15) quedaría fuera del horario del lunes (12:00–18:00). Ajusta o desactiva la franja primero.';
  assert.equal(extraerMensajeError({ response: { status: 409, data: { detail: franjaDelHorario } } }, 'Error genérico'), franjaDelHorario);
});

test('extraerMensajeError: un 500 o un error de red nunca expone el detalle crudo', () => {
  assert.equal(extraerMensajeError({ response: { status: 500, data: { detail: 'stack' } } }, 'Genérico'), 'Genérico');
  assert.equal(extraerMensajeError(new Error('Network Error'), 'Genérico'), 'Genérico');
});

// --- Políticas ---

test('politicasAFormulario y formularioAPoliticas: ida y vuelta entre números y texto', () => {
  const dto = { antelacionMinimaMinutos: 120, antelacionMaximaDias: 30, limiteCancelacionMinutos: 90,
    ventanaCheckInMinutos: 15, maxReservasVigentesPorTelefono: 2, maxPersonasPorReserva: 8 };
  const formulario = politicasAFormulario(dto);
  assert.equal(formulario.limiteCancelacionMinutos, '90');
  assert.deepEqual(formularioAPoliticas(formulario), dto);
  assert.equal(CAMPOS_POLITICAS.length, 6); // seis parámetros: sin tolerancia de no-show
});

test('politicasIguales: ignora espacios y compara los seis campos', () => {
  assert.equal(politicasIguales(VALIDAS, { ...VALIDAS, maxPersonasPorReserva: ' 8 ' }), true);
  assert.equal(politicasIguales(VALIDAS, { ...VALIDAS, maxPersonasPorReserva: '9' }), false);
});

test('validarPoliticas: los valores por defecto son válidos', () => {
  assert.deepEqual(validarPoliticas(VALIDAS), {});
  assert.deepEqual(validarPoliticas(politicasAFormulario({
    antelacionMinimaMinutos: POLITICAS_POR_DEFECTO.antelacionMinimaMinutos,
    antelacionMaximaDias: POLITICAS_POR_DEFECTO.antelacionMaximaDias,
    limiteCancelacionMinutos: POLITICAS_POR_DEFECTO.limiteCancelacionMinutos,
    ventanaCheckInMinutos: 15,
    maxReservasVigentesPorTelefono: 2,
    maxPersonasPorReserva: POLITICAS_POR_DEFECTO.maxPersonas,
  })), {});
});

test('validarPoliticas: espeja los rangos y mensajes del servidor', () => {
  assert.equal(validarPoliticas({ ...VALIDAS, antelacionMaximaDias: '0' }).antelacionMaximaDias, 'La antelación máxima debe estar entre 1 y 365 días.');
  assert.equal(validarPoliticas({ ...VALIDAS, antelacionMaximaDias: '366' }).antelacionMaximaDias, 'La antelación máxima debe estar entre 1 y 365 días.');
  assert.equal(validarPoliticas({ ...VALIDAS, ventanaCheckInMinutos: '121' }).ventanaCheckInMinutos, 'La ventana de check-in debe estar entre 0 y 120 minutos.');
  assert.equal(validarPoliticas({ ...VALIDAS, maxReservasVigentesPorTelefono: '11' }).maxReservasVigentesPorTelefono, 'El máximo de reservas vigentes por teléfono debe estar entre 1 y 10.');
  assert.equal(validarPoliticas({ ...VALIDAS, maxPersonasPorReserva: '51' }).maxPersonasPorReserva, 'El tamaño máximo de grupo debe estar entre 1 y 50 personas.');
  assert.equal(validarPoliticas({ ...VALIDAS, maxPersonasPorReserva: '50' }).maxPersonasPorReserva, undefined);
});

test('validarPoliticas: la antelación mínima debe ser menor que la máxima y el límite de cancelación no puede superarla', () => {
  // Máxima de 1 día = 1440 minutos.
  const unDia = { ...VALIDAS, antelacionMaximaDias: '1' };
  assert.equal(validarPoliticas({ ...unDia, antelacionMinimaMinutos: '1440', limiteCancelacionMinutos: '0' }).antelacionMinimaMinutos,
    'La antelación mínima debe ser menor que la antelación máxima.');
  assert.deepEqual(validarPoliticas({ ...unDia, antelacionMinimaMinutos: '1439', limiteCancelacionMinutos: '1440' }), {});
  assert.equal(validarPoliticas({ ...unDia, antelacionMinimaMinutos: '0', limiteCancelacionMinutos: '1441' }).limiteCancelacionMinutos,
    'El límite de cancelación no puede superar la antelación máxima.');
});

test('validarPoliticas: vacío, decimales, negativos y letras piden un entero', () => {
  for (const texto of ['', '2.5', '-1', 'abc', '1e2']) {
    assert.ok(validarPoliticas({ ...VALIDAS, ventanaCheckInMinutos: texto }).ventanaCheckInMinutos, texto);
  }
});

test('textoLegiblePolitica: minutos en horas y días en días, junto a la unidad', () => {
  const porClave = (clave) => CAMPOS_POLITICAS.find((c) => c.clave === clave);
  assert.equal(textoLegiblePolitica(porClave('antelacionMinimaMinutos'), '120'), '= 2 horas');
  assert.equal(textoLegiblePolitica(porClave('antelacionMinimaMinutos'), '90'), '= 90 minutos');
  assert.equal(textoLegiblePolitica(porClave('limiteCancelacionMinutos'), '60'), '= 1 hora');
  assert.equal(textoLegiblePolitica(porClave('antelacionMaximaDias'), '30'), '= 30 días');
  assert.equal(textoLegiblePolitica(porClave('antelacionMaximaDias'), '1'), '= 1 día');
  assert.equal(textoLegiblePolitica(porClave('maxPersonasPorReserva'), '8'), '');
  assert.equal(textoLegiblePolitica(porClave('antelacionMinimaMinutos'), 'abc'), '');
});

// --- Horario ---

test('horarioAFormulario y armarCuerpoHorario: "HH:mm:ss" del servidor <-> "HH:mm" del campo', () => {
  const formulario = horarioAFormulario({ diaSemana: 1, abierto: true, horaApertura: '12:00:00', horaCierre: '22:30:00' });
  assert.deepEqual(formulario, { diaSemana: 1, abierto: true, apertura: '12:00', cierre: '22:30' });
  assert.deepEqual(armarCuerpoHorario(formulario), { abierto: true, horaApertura: '12:00:00', horaCierre: '22:30:00' });
  assert.equal(horariosIguales(formulario, { ...formulario }), true);
  assert.equal(horariosIguales(formulario, { ...formulario, cierre: '23:00' }), false);
});

test('validarHorarioDia: abierto exige apertura anterior al cierre; cerrado no valida las horas', () => {
  assert.deepEqual(validarHorarioDia({ abierto: true, apertura: '12:00', cierre: '22:30' }), {});
  assert.equal(validarHorarioDia({ abierto: true, apertura: '22:30', cierre: '12:00' }).horaApertura,
    'La hora de apertura debe ser anterior a la hora de cierre.');
  assert.equal(validarHorarioDia({ abierto: true, apertura: '12:00', cierre: '12:00' }).horaApertura,
    'La hora de apertura debe ser anterior a la hora de cierre.');
  assert.ok(validarHorarioDia({ abierto: true, apertura: '', cierre: '12:00' }).horaApertura);
  assert.deepEqual(validarHorarioDia({ abierto: false, apertura: '', cierre: '' }), {});
});

// --- Días cerrados ---

test('validarDiaCerrado: fecha de hoy en adelante y motivo de hasta 200 caracteres', () => {
  const hoy = '2026-12-01';
  assert.deepEqual(validarDiaCerrado({ fecha: '2026-12-01', motivo: '' }, hoy), {});
  assert.deepEqual(validarDiaCerrado({ fecha: '2026-12-25', motivo: 'x'.repeat(200) }, hoy), {});
  assert.equal(validarDiaCerrado({ fecha: '2026-11-30', motivo: '' }, hoy).fecha, 'La fecha no puede ser en el pasado.');
  assert.equal(validarDiaCerrado({ fecha: '', motivo: '' }, hoy).fecha, 'La fecha es obligatoria.');
  assert.equal(validarDiaCerrado({ fecha: '2026-12-25', motivo: 'x'.repeat(201) }, hoy).motivo, 'El motivo no puede superar los 200 caracteres.');
});

test('armarCuerpoDiaCerrado: recorta el motivo y manda null si queda vacío', () => {
  assert.deepEqual(armarCuerpoDiaCerrado({ fecha: '2026-12-25', motivo: '  Navidad ' }), { fecha: '2026-12-25', motivo: 'Navidad' });
  assert.deepEqual(armarCuerpoDiaCerrado({ fecha: '2026-12-25', motivo: '   ' }), { fecha: '2026-12-25', motivo: null });
});

// --- (d) La antelación del personal sale de la configuración, no de valores fijos ---

test('esFranjaSoloPersonal: usa la antelación mínima configurada; sin ella, el valor por defecto', () => {
  const franja = { horaInicio: '12:00:00', horaFin: '15:00:00' };
  const base = { fecha: '2026-12-01', hoy: '2026-12-01', franja };
  // Por defecto (120 min): a las 10:30 ya falta menos de 2 h.
  assert.equal(esFranjaSoloPersonal({ ...base, hora: '10:30' }), true);
  // Con 30 minutos de antelación mínima, a las 10:30 todavía la puede reservar la web.
  assert.equal(esFranjaSoloPersonal({ ...base, hora: '10:30', antelacionMinimaMinutos: 30 }), false);
  assert.equal(esFranjaSoloPersonal({ ...base, hora: '11:31', antelacionMinimaMinutos: 30 }), true);
  // Con 0 (igual que el servidor: el inicio exacto todavía se puede reservar), solo cuenta la que ya empezó.
  assert.equal(esFranjaSoloPersonal({ ...base, hora: '12:00', antelacionMinimaMinutos: 0 }), false);
  assert.equal(esFranjaSoloPersonal({ ...base, hora: '12:01', antelacionMinimaMinutos: 0 }), true);
});

test('rangoFechasReserva y fechaEnRango: la ventana sale de la antelación máxima configurada', () => {
  assert.deepEqual(rangoFechasReserva('2026-11-11', 7), { min: '2026-11-11', max: '2026-11-18' });
  assert.equal(fechaEnRango('2026-11-18', '2026-11-11', 7), true);
  assert.equal(fechaEnRango('2026-11-19', '2026-11-11', 7), false);
  assert.equal(fechaEnRango('2027-01-10', '2026-11-11', 90), true);
});

test('los textos del personal usan los valores recibidos, no 30 días ni 2 horas', () => {
  assert.equal(mensajeFechaFueraDeRango(45), 'Elige una fecha desde hoy hasta 45 días adelante.');
  assert.equal(mensajeFechaFueraDeRango(1), 'Elige una fecha desde hoy hasta 1 día adelante.');
  assert.match(ayudaNuevoHorario(10), /hasta 10 días adelante/);
  assert.match(avisoFranjaSoloPersonal('90 minutos'), /faltan menos de 90 minutos/);
});

test('el frontend del personal no deja valores fijos de antelación en el código', () => {
  const archivos = [];
  (function recorrer(carpeta) {
    for (const entrada of fs.readdirSync(carpeta, { withFileTypes: true })) {
      const ruta = path.join(carpeta, entrada.name);
      if (entrada.isDirectory()) recorrer(ruta);
      else if (/\.(js|jsx)$/.test(entrada.name) && !entrada.name.endsWith('.test.js')) archivos.push(ruta);
    }
  }(CARPETA));

  for (const archivo of archivos) {
    const texto = fs.readFileSync(archivo, 'utf8');
    const nombre = path.basename(archivo);
    assert.doesNotMatch(texto, /MINUTOS_ANTELACION_WEB|DIAS_VENTANA/, `${nombre}: constantes fijas de antelación`);
    assert.doesNotMatch(texto, /hasta 30 días|menos de 2 horas/, `${nombre}: texto con valores fijos`);
  }
});
