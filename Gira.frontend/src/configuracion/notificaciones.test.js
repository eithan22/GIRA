import test from 'node:test';
import assert from 'node:assert/strict';
import { agregarNotificacion, fechaCorta, marcarLeidaLocal } from './bandejaNotificaciones.js';

const vacia = { notificaciones: [], noLeidas: 0 };
const aviso = { id: 'a', titulo: 'Stock bajo: Arroz', leida: false };

test('una notificación nueva va primero y sube el contador', () => {
  const bandeja = agregarNotificacion({ notificaciones: [{ id: 'b', leida: true }], noLeidas: 0 }, aviso);
  assert.equal(bandeja.notificaciones[0].id, 'a');
  assert.equal(bandeja.noLeidas, 1);
});

test('la misma notificación dos veces no se duplica', () => {
  const bandeja = agregarNotificacion(agregarNotificacion(vacia, aviso), aviso);
  assert.equal(bandeja.notificaciones.length, 1);
  assert.equal(bandeja.noLeidas, 1);
});

test('la fecha del servidor (UTC sin Z) se muestra en hora de Santo Domingo', () => {
  assert.equal(fechaCorta('2026-11-26T18:05:00'), '26/11, 14:05');
});

test('marcar leída baja el contador una sola vez', () => {
  const una = agregarNotificacion(vacia, aviso);
  const leida = marcarLeidaLocal(marcarLeidaLocal(una, 'a'), 'a');
  assert.equal(leida.noLeidas, 0);
  assert.equal(leida.notificaciones[0].leida, true);
});
