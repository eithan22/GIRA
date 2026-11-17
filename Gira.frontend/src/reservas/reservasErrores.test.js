// Pruebas de funciones puras de extracción de errores del servidor (Fase 1, GI-226/GI-404). Se corren con
// el ejecutor de Node, sin instalar nada:
//   node --test src/reservas/reservasErrores.test.js
import test from 'node:test';
import assert from 'node:assert/strict';
import { CODIGOS_ERROR, codigoDeError, errorPorCampo, extraerMensajeError } from './reservasErrores.js';

function errorConRespuesta(status, data) {
  return { response: { status, data } };
}

test('codigoDeError: devuelve el código cuando el servidor lo manda', () => {
  const error = errorConRespuesta(409, { detail: 'Ya tienes 2 reservas vigentes...', codigo: 'LIMITE_VIGENTES' });
  assert.equal(codigoDeError(error), 'LIMITE_VIGENTES');
  assert.equal(codigoDeError(error), CODIGOS_ERROR.LIMITE_VIGENTES);
});

test('codigoDeError: null si el servidor no manda código (errores todavía sin asignar)', () => {
  const error = errorConRespuesta(409, { detail: 'La mesa ya está reservada en esa fecha y franja.' });
  assert.equal(codigoDeError(error), null);
});

test('codigoDeError: null si no hay respuesta del servidor (error de red)', () => {
  assert.equal(codigoDeError(new Error('Network Error')), null);
  assert.equal(codigoDeError(undefined), null);
});

test('codigoDeError: ignora un código que no sea string', () => {
  const error = errorConRespuesta(409, { detail: 'x', codigo: 123 });
  assert.equal(codigoDeError(error), null);
});

test('codigoDeError: un mensaje reformulado no rompe la clasificación por código', () => {
  // El mismo código, con un `detail` distinto al que el frontend conocía (p. ej. máximo 1 o 3 en vez de 2):
  // el código sigue siendo la fuente de verdad, no el texto.
  const conUno = errorConRespuesta(409, { detail: 'Ya tienes 1 reserva vigente con este teléfono; cancela una para reservar otra.', codigo: 'LIMITE_VIGENTES' });
  const conTres = errorConRespuesta(409, { detail: 'Ya tienes 3 reservas vigentes con este teléfono; cancela una para reservar otra.', codigo: 'LIMITE_VIGENTES' });
  assert.equal(codigoDeError(conUno), CODIGOS_ERROR.LIMITE_VIGENTES);
  assert.equal(codigoDeError(conTres), CODIGOS_ERROR.LIMITE_VIGENTES);
});

test('extraerMensajeError: devuelve el detail en 400/404/409/429', () => {
  assert.equal(extraerMensajeError(errorConRespuesta(409, { detail: 'conflicto' }), 'default'), 'conflicto');
  assert.equal(extraerMensajeError(errorConRespuesta(400, { detail: 'invalido' }), 'default'), 'invalido');
});

test('extraerMensajeError: cae al mensaje por defecto en 500 o sin respuesta', () => {
  assert.equal(extraerMensajeError(errorConRespuesta(500, { detail: 'no exponer' }), 'default'), 'default');
  assert.equal(extraerMensajeError(new Error('timeout'), 'default'), 'default');
});

test('errorPorCampo: arma { campo: mensaje } desde un ValidationProblem 400', () => {
  const error = errorConRespuesta(400, { errors: { toleranciaNoShowMinutos: ['La tolerancia de no-show es obligatoria.'] } });
  assert.deepEqual(errorPorCampo(error), { toleranciaNoShowMinutos: 'La tolerancia de no-show es obligatoria.' });
});

test('errorPorCampo: {} si no es un error 400 de validación', () => {
  assert.deepEqual(errorPorCampo(errorConRespuesta(409, { detail: 'x' })), {});
  assert.deepEqual(errorPorCampo(undefined), {});
});
