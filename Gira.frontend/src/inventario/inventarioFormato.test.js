// Pruebas de funciones puras de Inventario: node --test src/inventario/inventarioFormato.test.js
import test from 'node:test';
import assert from 'node:assert/strict';
import { aNumero, armarMovimiento, fecha, hoyLocal, mensajeError, sumarDias, tieneRol } from './inventarioFormato.js';

test('fecha convierte AAAA-MM-DD sin mover el día', () => {
  assert.equal(fecha('2026-11-01'), '01/11/2026');
  assert.equal(fecha(null), '—');
});

test('sumarDias cruza meses y años', () => {
  assert.equal(sumarDias('2026-12-30', 3), '2027-01-02');
  assert.equal(sumarDias('2026-03-01', -1), '2026-02-28');
});

test('hoyLocal usa la hora de Santo Domingo', () => {
  // 02:00 UTC del 18 todavía es el 17 en Santo Domingo (UTC−4).
  assert.equal(hoyLocal(new Date('2026-11-18T02:00:00Z')), '2026-11-17');
});

test('aNumero acepta coma decimal y rechaza texto', () => {
  assert.equal(aNumero('2,5'), 2.5);
  assert.equal(aNumero(''), null);
  assert.equal(aNumero('abc'), null);
});

test('armarMovimiento: el ajuste negativo resta y la merma va positiva', () => {
  assert.equal(armarMovimiento({ tipo: 'Ajuste', cantidad: '3', sentido: '-' }).cantidad, -3);
  assert.equal(armarMovimiento({ tipo: 'Merma', cantidad: '-3', sentido: '-' }).cantidad, 3);
  const entrada = armarMovimiento({ tipo: 'Entrada', cantidad: '5', costoUnitario: '45', fechaVencimiento: '2026-11-20', motivo: '  ' });
  assert.equal(entrada.costoUnitario, 45);
  assert.equal(entrada.fechaVencimiento, '2026-11-20');
  assert.equal(entrada.motivo, null);
  assert.equal(armarMovimiento({ tipo: 'Merma', cantidad: '1', costoUnitario: '9', fechaVencimiento: '2026-11-20' }).costoUnitario, null);
});

test('mensajeError muestra el detalle del servidor solo en 400, 404 y 409', () => {
  assert.equal(mensajeError({ response: { status: 409, data: { detail: 'Stock insuficiente' } } }), 'Stock insuficiente');
  assert.equal(mensajeError({ response: { status: 400, data: { errors: { cantidad: ['Debe ser mayor que cero'] } } } }), 'Debe ser mayor que cero');
  assert.equal(mensajeError({ response: { status: 403 } }), 'No tienes permiso para hacer esto.');
  assert.equal(mensajeError({ response: { status: 500, data: { detail: 'stack' } } }, 'Falló'), 'Falló');
});

test('tieneRol', () => {
  assert.equal(tieneRol({ roles: ['Cocina'] }, ['Gerente', 'Cocina']), true);
  assert.equal(tieneRol(null, ['Gerente']), false);
});
