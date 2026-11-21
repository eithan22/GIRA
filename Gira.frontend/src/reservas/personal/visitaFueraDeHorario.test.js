// Visita sin reserva fuera de horario y en franjas anteriores (corrección posterior a la prueba completa): el servidor solo
// la acepta con el restaurante abierto y en la franja más reciente de las que ya empezaron.
import test from 'node:test';
import assert from 'node:assert/strict';
import { estadoRestauranteAhora, franjasParaVisita } from './reservasPersonalFormato.js';
import {
  TEXTO_FRANJA_NO_EMPEZO,
  TEXTO_FRANJA_TERMINADA,
  TEXTO_RESTAURANTE_CERRADO_HOY,
  textoRestauranteCerradoAhora,
} from './reservasPersonalTextos.js';

// Una franja por turno, con el horario real 12:00–22:30 todos los días.
const FRANJAS = [
  { id: 'almuerzo', nombre: 'Almuerzo', horaInicio: '12:00:00', horaFin: '13:45:00', activa: true },
  { id: 'tarde', nombre: 'Tarde', horaInicio: '14:00:00', horaFin: '15:45:00', activa: true },
  { id: 'cena', nombre: 'Cena', horaInicio: '18:30:00', horaFin: '20:15:00', activa: true },
  { id: 'noche', nombre: 'Noche', horaInicio: '20:30:00', horaFin: '22:15:00', activa: true },
  { id: 'vieja', nombre: 'Vieja', horaInicio: '10:00:00', horaFin: '11:00:00', activa: false },
];
const HORARIOS = Array.from({ length: 7 }, (_, dia) => ({ diaSemana: dia, abierto: true, horaApertura: '12:00:00', horaCierre: '22:30:00' }));
const HOY = '2026-11-14'; // sábado (diaSemana 6)

const elegibles = (hora) => franjasParaVisita(FRANJAS, hora).filter((f) => f.elegible).map((f) => f.id);

test('franjasParaVisita deja UNA sola franja elegible en cualquier momento del día', () => {
  assert.deepEqual(elegibles('12:00'), ['almuerzo']);
  assert.deepEqual(elegibles('13:00'), ['almuerzo']);
  assert.deepEqual(elegibles('14:00'), ['tarde']); // el cambio de franja: ya no vale el almuerzo
  assert.deepEqual(elegibles('16:30'), ['tarde']); // entre franjas: la última que empezó
  assert.deepEqual(elegibles('20:45'), ['noche']);
  assert.deepEqual(elegibles('22:20'), ['noche']);
});

test('franjasParaVisita: ninguna elegible si todavía no empezó ninguna; las inactivas no cuentan', () => {
  assert.deepEqual(elegibles('08:00'), []);
  assert.ok(franjasParaVisita(FRANJAS, '16:30').every((f) => f.id !== 'vieja'));
});

test('franjasParaVisita: las no elegibles llevan su estado como motivo (terminada / no empezó)', () => {
  const r = franjasParaVisita(FRANJAS, '16:30');
  assert.deepEqual(r.map((f) => [f.id, f.estado, f.elegible]), [
    ['almuerzo', 'terminada', false], ['tarde', 'terminada', true], ['cena', 'noEmpezo', false], ['noche', 'noEmpezo', false],
  ]);
});

test('los textos de franja ya no dicen "(el servidor lo permite)"', () => {
  assert.equal(TEXTO_FRANJA_TERMINADA, 'Ya terminó');
  assert.equal(TEXTO_FRANJA_NO_EMPEZO, 'Todavía no empieza');
  assert.doesNotMatch(`${TEXTO_FRANJA_TERMINADA} ${TEXTO_FRANJA_NO_EMPEZO}`, /servidor lo permite/);
});

test('estadoRestauranteAhora: abierto entre la apertura (inclusive) y el cierre (exclusivo)', () => {
  const e = (hora) => estadoRestauranteAhora({ horarios: HORARIOS, diasCerrados: [], hoy: HOY, hora }).estado;
  assert.equal(e('12:00'), 'abierto');
  assert.equal(e('18:00'), 'abierto');
  assert.equal(e('22:29'), 'abierto');
  assert.equal(e('22:30'), 'cerradoAhora');
  assert.equal(e('23:26'), 'cerradoAhora'); // el caso real de la captura
  assert.equal(e('11:59'), 'cerradoAhora');
  assert.equal(e('03:00'), 'cerradoAhora');
});

test('estadoRestauranteAhora: devuelve el horario del día para el mensaje', () => {
  const r = estadoRestauranteAhora({ horarios: HORARIOS, diasCerrados: [], hoy: HOY, hora: '23:26' });
  assert.deepEqual(r, { estado: 'cerradoAhora', apertura: '12:00', cierre: '22:30' });
  assert.equal(
    textoRestauranteCerradoAhora(r.apertura, r.cierre),
    'El restaurante está cerrado ahora (horario de hoy: 12:00–22:30). No se pueden registrar visitas sin reserva fuera de horario.',
  );
});

test('estadoRestauranteAhora: día de la semana cerrado y fecha cerrada → cerrado hoy', () => {
  const sabadoCerrado = HORARIOS.map((h) => (h.diaSemana === 6 ? { ...h, abierto: false } : h));
  assert.equal(estadoRestauranteAhora({ horarios: sabadoCerrado, diasCerrados: [], hoy: HOY, hora: '13:00' }).estado, 'cerradoHoy');
  assert.equal(estadoRestauranteAhora({ horarios: HORARIOS, diasCerrados: [{ fecha: '2026-11-14' }], hoy: HOY, hora: '13:00' }).estado, 'cerradoHoy');
  // La fecha cerrada puede venir con hora ("2026-11-14T00:00:00"): solo cuenta el día.
  assert.equal(estadoRestauranteAhora({ horarios: HORARIOS, diasCerrados: [{ fecha: '2026-11-14T00:00:00' }], hoy: HOY, hora: '13:00' }).estado, 'cerradoHoy');
  // Otra fecha cerrada no afecta a hoy.
  assert.equal(estadoRestauranteAhora({ horarios: HORARIOS, diasCerrados: [{ fecha: '2026-11-15' }], hoy: HOY, hora: '13:00' }).estado, 'abierto');
  assert.match(TEXTO_RESTAURANTE_CERRADO_HOY, /cerrado hoy/);
});

test('estadoRestauranteAhora: usa el horario del día que corresponde a hoy', () => {
  // 2026-11-14 es sábado (6): si solo el sábado abre de 18:00 a 23:00, a las 22:30 sigue abierto.
  const horarios = HORARIOS.map((h) => (h.diaSemana === 6 ? { ...h, horaApertura: '18:00:00', horaCierre: '23:00:00' } : h));
  assert.equal(estadoRestauranteAhora({ horarios, diasCerrados: [], hoy: HOY, hora: '22:30' }).estado, 'abierto');
  assert.equal(estadoRestauranteAhora({ horarios, diasCerrados: [], hoy: HOY, hora: '17:59' }).estado, 'cerradoAhora');
});
