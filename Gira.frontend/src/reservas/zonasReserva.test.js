import test from 'node:test';
import assert from 'node:assert/strict';
import {
  hayEleccionDeZona,
  nombreZonaElegida,
  opcionesSelectorZona,
  zonaSigueDisponible,
  zonasDeFranja,
} from './zonasReserva.js';
import { textoZonaTicket, ETIQUETA_SIN_PREFERENCIA, TEXTO_TICKET_SIN_ZONA } from './reservasTextos.js';

const SALON = { id: 'z-salon', nombre: 'Salón' };
const TERRAZA = { id: 'z-terraza', nombre: 'Terraza' };
const franja = (zonas) => ({ id: 'f1', nombre: 'Almuerzo', zonasDisponibles: zonas });

test('zonasDeFranja: devuelve la lista del servidor y [] si falta o no es una lista', () => {
  assert.deepEqual(zonasDeFranja(franja([SALON])), [SALON]);
  assert.deepEqual(zonasDeFranja({ id: 'f1' }), []);
  assert.deepEqual(zonasDeFranja(null), []);
  assert.deepEqual(zonasDeFranja({ zonasDisponibles: 'x' }), []);
});

test('hayEleccionDeZona: solo con más de una zona con mesa libre', () => {
  assert.equal(hayEleccionDeZona(franja([SALON, TERRAZA])), true);
  assert.equal(hayEleccionDeZona(franja([SALON])), false);
  assert.equal(hayEleccionDeZona(franja([])), false);
});

test('opcionesSelectorZona: "Sin preferencia" primero y solo zonas disponibles (las llenas no llegan)', () => {
  assert.deepEqual(opcionesSelectorZona(franja([SALON, TERRAZA])), [
    { valor: '', etiqueta: ETIQUETA_SIN_PREFERENCIA },
    { valor: 'z-salon', etiqueta: 'Salón' },
    { valor: 'z-terraza', etiqueta: 'Terraza' },
  ]);
});

test('opcionesSelectorZona: con una sola zona no hay selector', () => {
  assert.deepEqual(opcionesSelectorZona(franja([SALON])), []);
});

test('zonaSigueDisponible: sin preferencia siempre vale; una zona llena en la franja nueva ya no', () => {
  assert.equal(zonaSigueDisponible(null, franja([SALON])), true);
  assert.equal(zonaSigueDisponible('', franja([])), true);
  assert.equal(zonaSigueDisponible('z-salon', franja([SALON, TERRAZA])), true);
  assert.equal(zonaSigueDisponible('z-salon', franja([TERRAZA])), false);
});

test('nombreZonaElegida: nombre de la zona elegida o null', () => {
  assert.equal(nombreZonaElegida(franja([SALON, TERRAZA]), 'z-terraza'), 'Terraza');
  assert.equal(nombreZonaElegida(franja([SALON]), null), null);
  assert.equal(nombreZonaElegida(franja([SALON]), 'z-otra'), null);
});

test('textoZonaTicket: con zona la nombra; sin zona avisa que se asigna en la zona disponible; nunca habla de número de mesa', () => {
  assert.match(textoZonaTicket('Terraza'), /Terraza/);
  assert.equal(textoZonaTicket(null), TEXTO_TICKET_SIN_ZONA);
  assert.equal(TEXTO_TICKET_SIN_ZONA, 'Te asignaremos una mesa en la zona disponible.');
  assert.doesNotMatch(textoZonaTicket('Terraza') + TEXTO_TICKET_SIN_ZONA, /mesa\s*(n[úu]mero|\d)|La mesa se asigna sola/i);
});
