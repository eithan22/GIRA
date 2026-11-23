import { test } from 'node:test';
import assert from 'node:assert/strict';
import {
  ESTADO_SUGERENCIA,
  ESTADO_TURNO,
  aEnvioTurno,
  diasDeSemana,
  etiquetaEstadoSugerencia,
  etiquetaEstadoTurno,
  horaCorta,
  lunesDe,
  puedeResolver,
  puedeMarcarPasado,
  puedeModificarTurno,
  sumarDias,
  textoDiferencia,
  turnosQueCubren,
  validarTurno,
} from './planificacionFormato.js';

test('lunesDe devuelve el lunes de la semana, también si la fecha es domingo', () => {
  assert.equal(lunesDe('2026-12-07'), '2026-12-07'); // lunes
  assert.equal(lunesDe('2026-12-13'), '2026-12-07'); // domingo de la misma semana
  assert.equal(lunesDe('2026-12-10'), '2026-12-07'); // jueves
});

test('diasDeSemana y sumarDias cruzan fin de mes sin depender de la zona horaria', () => {
  assert.deepEqual(diasDeSemana('2026-12-28').slice(0, 4), ['2026-12-28', '2026-12-29', '2026-12-30', '2026-12-31']);
  assert.equal(sumarDias('2026-12-31', 1), '2027-01-01');
  assert.equal(diasDeSemana('2026-12-07').length, 7);
});

test('horaCorta quita los segundos de la hora de la API', () => {
  assert.equal(horaCorta('09:30:00'), '09:30');
  assert.equal(horaCorta(null), '');
});

test('validarTurno exige empleado, cargo, fecha y un horario coherente', () => {
  assert.deepEqual(validarTurno({ empleadoId: 'e', cargoId: 'c', fecha: '2026-12-07', horaInicio: '09:00', horaFin: '13:00', notas: '' }), {});

  const errores = validarTurno({ empleadoId: '', cargoId: '', fecha: '', horaInicio: '13:00', horaFin: '09:00', notas: '' });
  assert.ok(errores.empleadoId);
  assert.ok(errores.cargoId);
  assert.ok(errores.fecha);
  assert.match(errores.horaFin, /posterior/);
});

test('aEnvioTurno manda las horas como HH:mm:00 y las notas vacías como null', () => {
  const envio = aEnvioTurno({ empleadoId: 'e', cargoId: 'c', fecha: '2026-12-07', horaInicio: '09:00', horaFin: '13:00', notas: '  ' });
  assert.deepEqual(envio, { empleadoId: 'e', cargoId: 'c', fecha: '2026-12-07', horaInicio: '09:00:00', horaFin: '13:00:00', notas: null });
});

test('turnosQueCubren cuenta solo turnos vigentes que cubren la franja completa', () => {
  const franja = { fecha: '2026-12-07', cargoId: 'mesero', horaInicio: '10:00:00', horaFin: '12:00:00' };
  const turnos = [
    { fecha: '2026-12-07', cargoId: 'mesero', estado: ESTADO_TURNO.PROGRAMADO, horaInicio: '09:00:00', horaFin: '13:00:00' },
    { fecha: '2026-12-07', cargoId: 'mesero', estado: ESTADO_TURNO.CONFIRMADO, horaInicio: '10:00:00', horaFin: '12:00:00' },
    { fecha: '2026-12-07', cargoId: 'mesero', estado: ESTADO_TURNO.CANCELADO, horaInicio: '09:00:00', horaFin: '13:00:00' },
    { fecha: '2026-12-07', cargoId: 'mesero', estado: ESTADO_TURNO.PROGRAMADO, horaInicio: '11:00:00', horaFin: '13:00:00' },
    { fecha: '2026-12-08', cargoId: 'mesero', estado: ESTADO_TURNO.PROGRAMADO, horaInicio: '09:00:00', horaFin: '13:00:00' },
  ];
  assert.equal(turnosQueCubren(turnos, franja).length, 2);
});

test('textoDiferencia distingue faltantes, sobrantes y casos sin diferencia', () => {
  assert.equal(textoDiferencia(3), 'Faltan 3');
  assert.equal(textoDiferencia(-2), 'Sobran 2');
  assert.equal(textoDiferencia(0), 'Sin diferencia');
});

test('solo las sugerencias pendientes se pueden resolver', () => {
  assert.equal(puedeResolver({ estado: ESTADO_SUGERENCIA.PENDIENTE }), true);
  assert.equal(puedeResolver({ estado: ESTADO_SUGERENCIA.ACEPTADA }), false);
  assert.equal(puedeResolver({ estado: ESTADO_SUGERENCIA.VENCIDA }), false);
});

test('las etiquetas de estado cubren todos los valores', () => {
  assert.equal(etiquetaEstadoTurno(ESTADO_TURNO.CANCELADO), 'Cancelado');
  assert.equal(etiquetaEstadoSugerencia(ESTADO_SUGERENCIA.VENCIDA), 'Vencida');
  assert.equal(etiquetaEstadoSugerencia(99), '—');
});

test('puedeMarcarPasado solo para turnos vigentes de un día anterior a hoy (GI-380)', () => {
  const pasado = { fecha: '2026-12-01', estado: ESTADO_TURNO.PROGRAMADO };
  const hoy = { fecha: '2026-12-07', estado: ESTADO_TURNO.PROGRAMADO };
  const cancelado = { fecha: '2026-12-01', estado: ESTADO_TURNO.CANCELADO };
  assert.equal(puedeMarcarPasado(pasado, '2026-12-07'), true);
  assert.equal(puedeMarcarPasado(hoy, '2026-12-07'), false);
  assert.equal(puedeMarcarPasado(cancelado, '2026-12-07'), false);
});

test('puedeModificarTurno solo para turnos vigentes de hoy en adelante', () => {
  const futuro = { fecha: '2026-12-08', estado: ESTADO_TURNO.CONFIRMADO };
  const pasado = { fecha: '2026-12-01', estado: ESTADO_TURNO.PROGRAMADO };
  assert.equal(puedeModificarTurno(futuro, '2026-12-07'), true);
  assert.equal(puedeModificarTurno(pasado, '2026-12-07'), false);
});
