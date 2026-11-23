import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  TODOS_LOS_DIAS,
  diasDelBloque,
  DIAS_EN_ORDEN,
  aEnvioDisponibilidad,
  nombreDia,
  ordenarDisponibilidad,
  validarDisponibilidad,
  validarSolicitudPropia,
  aEnvioSolicitudPropia,
} from './miPersonalFormato.js';

test('nombreDia usa 0 como domingo, según DayOfWeek de la API', () => {
  assert.equal(nombreDia(0), 'Domingo');
  assert.equal(nombreDia(1), 'Lunes');
  assert.equal(nombreDia(6), 'Sábado');
  assert.equal(nombreDia(9), '—');
});

test('ordenarDisponibilidad pone lunes primero y domingo al final, y luego por hora', () => {
  const bloques = [
    { diaSemana: 0, horaInicio: '08:00:00' },
    { diaSemana: 1, horaInicio: '14:00:00' },
    { diaSemana: 1, horaInicio: '06:00:00' },
  ];
  const ordenados = ordenarDisponibilidad(bloques);
  assert.deepEqual(ordenados.map((b) => `${b.diaSemana} ${b.horaInicio}`), ['1 06:00:00', '1 14:00:00', '0 08:00:00']);
  assert.equal(DIAS_EN_ORDEN[0], 1);
});

test('validarDisponibilidad exige día y horario coherente', () => {
  assert.deepEqual(validarDisponibilidad({ diaSemana: '1', horaInicio: '06:00', horaFin: '14:00' }), {});

  const errores = validarDisponibilidad({ diaSemana: '', horaInicio: '14:00', horaFin: '06:00' });
  assert.ok(errores.diaSemana);
  assert.match(errores.horaFin, /posterior/);
});

test('aEnvioDisponibilidad manda el día como número y las horas como HH:mm:00', () => {
  assert.deepEqual(
    aEnvioDisponibilidad({ diaSemana: '3', horaInicio: '06:00', horaFin: '14:00' }),
    { diaSemana: 3, horaInicio: '06:00:00', horaFin: '14:00:00' });
});

test('validarSolicitudPropia exige fechas en orden y limita el motivo', () => {
  assert.deepEqual(validarSolicitudPropia({ fechaDesde: '2026-12-07', fechaHasta: '2026-12-08', motivo: 'Viaje' }), {});

  const errores = validarSolicitudPropia({ fechaDesde: '2026-12-10', fechaHasta: '2026-12-08', motivo: '' });
  assert.match(errores.fechaHasta, /anterior/);

  const sinFechas = validarSolicitudPropia({ fechaDesde: '', fechaHasta: '', motivo: 'x' });
  assert.ok(sinFechas.fechaDesde);
  assert.ok(sinFechas.fechaHasta);
  assert.ok(validarSolicitudPropia({ fechaDesde: '2026-12-07', fechaHasta: '2026-12-07', motivo: 'x'.repeat(251) }).motivo);
});

test('aEnvioSolicitudPropia manda el tipo como número y el motivo vacío como null', () => {
  assert.deepEqual(
    aEnvioSolicitudPropia({ fechaDesde: '2026-12-07', fechaHasta: '2026-12-08', tipo: '1', motivo: '  ' }),
    { fechaDesde: '2026-12-07', fechaHasta: '2026-12-08', tipo: 1, motivo: null });
});

test('diasDelBloque aplica el bloque a todos los días o solo al elegido', () => {
  assert.deepEqual(diasDelBloque(TODOS_LOS_DIAS), [0, 1, 2, 3, 4, 5, 6]);
  assert.deepEqual(diasDelBloque('3'), [3]);
});
