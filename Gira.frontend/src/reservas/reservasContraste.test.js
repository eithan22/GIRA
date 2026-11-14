// Contraste (WCAG 2.1) de la franja no disponible de /reservar (GI-226): lee los colores reales de los CSS y
// comprueba que cada par texto/fondo llega a 4,5:1 y que el borde punteado llega a 3:1. Se corre con:
//   node --test src/reservas/reservasContraste.test.js
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

const CARPETA = path.dirname(fileURLToPath(import.meta.url));
const VARIABLES = fs.readFileSync(path.join(CARPETA, 'styles/00-variables.css'), 'utf8');
const SELECCION = fs.readFileSync(path.join(CARPETA, 'styles/04-seleccion.css'), 'utf8');

function color(variable) {
  const coincidencia = new RegExp(`--${variable}:\\s*(#[0-9a-fA-F]{6})\\b`).exec(VARIABLES);
  assert.ok(coincidencia, `falta --${variable} en 00-variables.css`);
  return coincidencia[1];
}

function luminancia(hex) {
  const [r, g, b] = [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16) / 255)
    .map((c) => (c <= 0.03928 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4));
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

function contraste(a, b) {
  const [claro, oscuro] = [luminancia(a), luminancia(b)].sort((x, y) => y - x);
  return (claro + 0.05) / (oscuro + 0.05);
}

test('el contraste de la fórmula es correcto (negro/blanco = 21:1)', () => {
  assert.equal(Math.round(contraste('#000000', '#ffffff')), 21);
});

test('franja no disponible: el nombre, el horario y el motivo llegan a 4,5:1 sobre su fondo', () => {
  const fondo = color('reservas-franja-off-fondo');
  for (const variable of ['reservas-franja-off-texto', 'reservas-franja-off-horario', 'reservas-franja-off-motivo']) {
    const ratio = contraste(color(variable), fondo);
    assert.ok(ratio >= 4.5, `${variable} sobre el fondo: ${ratio.toFixed(2)}:1`);
  }
});

test('franja no disponible: el badge "No disponible" llega a 4,5:1', () => {
  const ratio = contraste(color('reservas-franja-off-badge-texto'), color('reservas-franja-off-badge-fondo'));
  assert.ok(ratio >= 4.5, `badge: ${ratio.toFixed(2)}:1`);
});

test('franja no disponible: el borde punteado llega a 3:1 contra el blanco y contra su fondo', () => {
  const borde = color('reservas-franja-off-borde');
  assert.ok(contraste(borde, '#ffffff') >= 3, `borde/blanco: ${contraste(borde, '#ffffff').toFixed(2)}:1`);
  assert.ok(contraste(borde, color('reservas-franja-off-fondo')) >= 3, 'borde/fondo');
});

test('la regla de la franja no disponible usa esas variables y no baja la opacidad del texto', () => {
  const regla = /\.reservas-franja--deshabilitada[^{]*\{[^}]*\}/g;
  const bloques = SELECCION.match(regla).join('\n');
  for (const variable of [
    'reservas-franja-off-fondo', 'reservas-franja-off-borde', 'reservas-franja-off-texto', 'reservas-franja-off-horario',
  ]) {
    assert.match(SELECCION, new RegExp(`var\\(--${variable}\\)`), variable);
  }
  assert.doesNotMatch(bloques, /opacity/);
  assert.match(SELECCION, /\.reservas-franja-motivo\s*\{[^}]*var\(--reservas-franja-off-motivo\)/);
});
