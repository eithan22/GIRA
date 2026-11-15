// El límite de reservas vigentes por teléfono sale de las políticas públicas (GET /api/reservas/politicas): los textos del
// ticket y del Paso 2 ya no llevan el "2" escrito a mano y concuerdan en singular y plural.
import test from 'node:test';
import assert from 'node:assert/strict';
import { normalizarPoliticas, POLITICAS_POR_DEFECTO } from './reservasFormato.js';
import {
  textoLimiteVigentesDatos,
  textoLimiteVigentesTicket,
  textoReservasVigentes,
} from './reservasTextos.js';

const conMaximo = (n) => normalizarPoliticas({ maxReservasVigentesPorTelefono: n });

test('textoReservasVigentes: singular con 1 y plural con 2 o más', () => {
  assert.equal(textoReservasVigentes(1), '1 reserva vigente');
  assert.equal(textoReservasVigentes(2), '2 reservas vigentes');
  assert.equal(textoReservasVigentes(3), '3 reservas vigentes');
});

test('ticket: el máximo sale de las políticas (1, 2 y 3)', () => {
  assert.equal(textoLimiteVigentesTicket(conMaximo(1)), 'Hasta 1 reserva vigente por teléfono.');
  assert.equal(textoLimiteVigentesTicket(conMaximo(2)), 'Hasta 2 reservas vigentes por teléfono.');
  assert.equal(textoLimiteVigentesTicket(conMaximo(3)), 'Hasta 3 reservas vigentes por teléfono.');
});

test('Paso 2: el máximo sale de las políticas (1, 2 y 3)', () => {
  assert.equal(textoLimiteVigentesDatos(conMaximo(1)), 'Puedes tener hasta 1 reserva vigente con el mismo número.');
  assert.equal(textoLimiteVigentesDatos(conMaximo(2)), 'Puedes tener hasta 2 reservas vigentes con el mismo número.');
  assert.equal(textoLimiteVigentesDatos(conMaximo(3)), 'Puedes tener hasta 3 reservas vigentes con el mismo número.');
});

test('normalizarPoliticas: toma el máximo del servidor; si falta o viene mal, usa el valor por defecto (2)', () => {
  assert.equal(conMaximo(3).maxReservasVigentesPorTelefono, 3);
  assert.equal(normalizarPoliticas({}).maxReservasVigentesPorTelefono, 2);
  assert.equal(conMaximo(0).maxReservasVigentesPorTelefono, 2);
  assert.equal(conMaximo('3').maxReservasVigentesPorTelefono, 2);
  assert.equal(normalizarPoliticas(null).maxReservasVigentesPorTelefono, POLITICAS_POR_DEFECTO.maxReservasVigentesPorTelefono);
});

test('los textos no vuelven a escribir el número a mano: con máximo 1 no aparece "2"', () => {
  const textos = `${textoLimiteVigentesTicket(conMaximo(1))} ${textoLimiteVigentesDatos(conMaximo(1))}`;
  assert.doesNotMatch(textos, /\b2\b/);
  assert.doesNotMatch(textos, /reservas vigentes/);
});
