// Pruebas de funciones puras. Se corren con el ejecutor de Node, sin instalar nada:
//   node --test src/reservas/personal/reservasPersonalTextos.test.js
// No se importa desde ningún componente, así que Vite no las empaqueta.
import test from 'node:test';
import assert from 'node:assert/strict';
import { TEXTO_MESA_CON_VISITA, textoMesaConVisitaAccesible } from './reservasPersonalTextos.js';

test('textoMesaConVisitaAccesible devuelve la coma y el texto cuando hay visita', () => {
  assert.equal(textoMesaConVisitaAccesible(true), `, ${TEXTO_MESA_CON_VISITA}`);
});

test('textoMesaConVisitaAccesible devuelve vacío sin visita', () => {
  assert.equal(textoMesaConVisitaAccesible(false), '');
  assert.equal(textoMesaConVisitaAccesible(undefined), '');
});
