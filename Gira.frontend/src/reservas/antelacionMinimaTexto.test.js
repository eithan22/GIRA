// Antelación mínima en 0: «faltan menos de 0 minutos» y «con al menos 0 minutos de anticipación» sonaban mal. Con 0 los
// textos dicen que no hay antelación mínima (cliente) o que el cliente web solo pierde la franja cuando ya empezó (personal).
import test from 'node:test';
import assert from 'node:assert/strict';
import { POLITICAS_POR_DEFECTO } from './reservasFormato.js';
import { textoVentanaReserva } from './reservasTextos.js';
import { avisoFranjaSoloPersonal } from './personal/reservasPersonalTextos.js';

const politicas = (antelacionMinimaMinutos) => ({ ...POLITICAS_POR_DEFECTO, antelacionMinimaMinutos });

test('/reservar: la ventana de reserva con antelación mínima 0, 30, 60 y 120 minutos', () => {
  assert.equal(textoVentanaReserva(politicas(0)), 'Reservamos desde hoy hasta 30 días adelante y sin antelación mínima.');
  assert.equal(textoVentanaReserva(politicas(30)), 'Reservamos desde hoy hasta 30 días adelante y con al menos 30 minutos de anticipación.');
  assert.equal(textoVentanaReserva(politicas(60)), 'Reservamos desde hoy hasta 30 días adelante y con al menos 1 hora de anticipación.');
  assert.equal(textoVentanaReserva(politicas(120)), 'Reservamos desde hoy hasta 30 días adelante y con al menos 2 horas de anticipación.');
});

test('Nueva reserva (personal): el aviso de franja solo para el personal con antelación 0, 30, 60 y 120 minutos', () => {
  const cola = 'Puedes registrarla mientras no haya terminado.';
  const base = 'La franja de hoy está disponible solo para el personal: el cliente web ya no puede reservarla porque';
  assert.equal(avisoFranjaSoloPersonal(0), `${base} ya empezó. ${cola}`);
  assert.equal(avisoFranjaSoloPersonal(30), `${base} faltan menos de 30 minutos o ya empezó. ${cola}`);
  assert.equal(avisoFranjaSoloPersonal(60), `${base} faltan menos de 1 hora o ya empezó. ${cola}`);
  assert.equal(avisoFranjaSoloPersonal(120), `${base} faltan menos de 2 horas o ya empezó. ${cola}`);
});

test('con antelación 0 ningún texto dice «0 minutos»', () => {
  assert.doesNotMatch(`${textoVentanaReserva(politicas(0))} ${avisoFranjaSoloPersonal(0)}`, /0 minutos|0 horas/);
});
