// Pruebas de funciones puras de /reservar (GI-226): políticas, personas, franjas y textos. Se corren con el
// ejecutor de Node, sin instalar nada:
//   node --test src/reservas/reservasFormato.test.js
// No se importa desde ningún componente, así que Vite no las empaqueta.
import test from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import {
  MAXIMO_CHIPS_PERSONAS,
  MOTIVO_RESTAURANTE_CERRADO,
  POLITICAS_POR_DEFECTO,
  cantidadChipsPersonas,
  diaCerradoEnFranjas,
  estadoFranjaCliente,
  formatearDuracionMinutos,
  franjaSigueDisponible,
  generarDiasChip,
  normalizarPoliticas,
  obtenerLimiteFechas,
  usaCampoNumericoPersonas,
  validarPersonasCliente,
} from './reservasFormato.js';
import {
  textoGrupoGrande,
  textoLimiteCancelacion,
  textoMasDePersonas,
  textoVentanaReserva,
} from './reservasTextos.js';

const CARPETA = path.dirname(fileURLToPath(import.meta.url));
const HOY = { anio: 2026, mes: 11, dia: 1 }; // 1 de diciembre de 2026 (mes 0-based)

// --- Valores por defecto: un solo sitio, espejo de ParametrosReservaService.ValoresPorDefecto ---

test('POLITICAS_POR_DEFECTO: refleja los valores por defecto del backend (2 h, 30 días, 2 h, 8 personas)', () => {
  assert.equal(POLITICAS_POR_DEFECTO.antelacionMinimaMinutos, 120);
  assert.equal(POLITICAS_POR_DEFECTO.antelacionMaximaDias, 30);
  assert.equal(POLITICAS_POR_DEFECTO.limiteCancelacionMinutos, 120);
  assert.equal(POLITICAS_POR_DEFECTO.maxPersonas, 8);
  assert.deepEqual([...POLITICAS_POR_DEFECTO.diasCerrados], []);
});

test('los valores por defecto viven solo en reservasFormato.js y no quedan topes fijos en /reservar', () => {
  const archivos = [];
  (function recorrer(carpeta) {
    for (const entrada of fs.readdirSync(carpeta, { withFileTypes: true })) {
      const ruta = path.join(carpeta, entrada.name);
      if (entrada.isDirectory()) {
        if (entrada.name !== 'personal') recorrer(ruta); // el personal tiene su propia carpeta y sus propias pruebas
      } else if (/\.(js|jsx)$/.test(entrada.name) && !entrada.name.endsWith('.test.js')) {
        archivos.push(ruta);
      }
    }
  }(CARPETA));

  const conDefectos = archivos.filter((a) => /maxPersonas:\s*8\b/.test(fs.readFileSync(a, 'utf8')));
  assert.deepEqual(conDefectos.map((a) => path.basename(a)), ['reservasFormato.js']);

  for (const archivo of archivos) {
    const texto = fs.readFileSync(archivo, 'utf8');
    const nombre = path.basename(archivo);
    assert.doesNotMatch(texto, /sumarDiasLocal\([^)]*,\s*30\)/, `${nombre}: tope fijo de 30 días`);
    assert.doesNotMatch(texto, /MAX_PERSONAS_CHIPS/, `${nombre}: 8 chips fijos`);
    assert.doesNotMatch(texto, /TEXTO_(VENTANA_RESERVA|MAS_DE_8|LIMITE_CANCELACION)/, `${nombre}: textos con valores fijos`);
  }
});

// --- normalizarPoliticas ---

test('normalizarPoliticas: toma lo que llega del servidor y completa lo que falte o venga mal con el defecto', () => {
  assert.deepEqual(
    normalizarPoliticas({
      antelacionMinimaMinutos: 45, antelacionMaximaDias: 60, limiteCancelacionMinutos: 0, maxPersonas: 4,
      diasCerrados: ['2026-12-25'],
    }),
    {
      antelacionMinimaMinutos: 45, antelacionMaximaDias: 60, limiteCancelacionMinutos: 0, maxPersonas: 4,
      diasCerrados: ['2026-12-25'],
    },
  );
  assert.equal(normalizarPoliticas(null), POLITICAS_POR_DEFECTO);
  assert.equal(normalizarPoliticas({ antelacionMaximaDias: 'x', maxPersonas: 0 }).antelacionMaximaDias, 30);
  assert.equal(normalizarPoliticas({ antelacionMaximaDias: 'x', maxPersonas: 0 }).maxPersonas, 8);
  assert.deepEqual(normalizarPoliticas({ diasCerrados: 'no' }).diasCerrados, []);
});

// --- Textos desde las políticas ---

test('formatearDuracionMinutos: horas exactas en horas, el resto en minutos', () => {
  assert.equal(formatearDuracionMinutos(120), '2 horas');
  assert.equal(formatearDuracionMinutos(60), '1 hora');
  assert.equal(formatearDuracionMinutos(90), '90 minutos');
  assert.equal(formatearDuracionMinutos(1), '1 minuto');
});

test('los textos usan los valores de las políticas, no 30 días / 2 horas / 8 personas', () => {
  const politicas = { ...POLITICAS_POR_DEFECTO, antelacionMaximaDias: 45, antelacionMinimaMinutos: 90, limiteCancelacionMinutos: 180, maxPersonas: 20 };

  assert.equal(textoVentanaReserva(politicas), 'Reservamos desde hoy hasta 45 días adelante y con al menos 90 minutos de anticipación.');
  assert.match(textoLimiteCancelacion(politicas), /^Cancelación gratis hasta 3 horas antes de la franja\./);
  assert.match(textoMasDePersonas(politicas), /^¿Más de 20\?/);
  assert.match(textoGrupoGrande(politicas), /^Para grupos de más de 20 personas, llama al restaurante\./);
});

test('los textos con los valores por defecto dicen lo mismo que decían antes', () => {
  assert.equal(textoVentanaReserva(POLITICAS_POR_DEFECTO), 'Reservamos desde hoy hasta 30 días adelante y con al menos 2 horas de anticipación.');
  assert.match(textoLimiteCancelacion(POLITICAS_POR_DEFECTO), /^Cancelación gratis hasta 2 horas antes de la franja\./);
  assert.match(textoMasDePersonas(POLITICAS_POR_DEFECTO), /^¿Más de 8\?/);
});

test('textoVentanaReserva usa el singular con 1 día; textoLimiteCancelacion con 0 minutos habla del inicio de la franja', () => {
  assert.match(textoVentanaReserva({ ...POLITICAS_POR_DEFECTO, antelacionMaximaDias: 1 }), /hasta 1 día adelante/);
  assert.match(textoLimiteCancelacion({ ...POLITICAS_POR_DEFECTO, limiteCancelacionMinutos: 0 }), /^Puedes cancelar hasta el inicio de la franja\./);
});

// --- Fechas: el tope sale de la antelación máxima ---

test('obtenerLimiteFechas: el máximo es hoy + la antelación máxima de las políticas', () => {
  assert.deepEqual(obtenerLimiteFechas(POLITICAS_POR_DEFECTO, HOY), { min: '2026-12-01', max: '2026-12-31' });
  assert.deepEqual(obtenerLimiteFechas({ ...POLITICAS_POR_DEFECTO, antelacionMaximaDias: 7 }, HOY), { min: '2026-12-01', max: '2026-12-08' });
  assert.deepEqual(obtenerLimiteFechas({ ...POLITICAS_POR_DEFECTO, antelacionMaximaDias: 90 }, HOY), { min: '2026-12-01', max: '2027-03-01' });
});

test('generarDiasChip: 6 chips por defecto, pero nunca más allá de la antelación máxima', () => {
  assert.equal(generarDiasChip(5, POLITICAS_POR_DEFECTO, HOY).length, 6);
  const corto = generarDiasChip(5, { ...POLITICAS_POR_DEFECTO, antelacionMaximaDias: 2 }, HOY);
  assert.deepEqual(corto.map((d) => d.iso), ['2026-12-01', '2026-12-02', '2026-12-03']);
});

// --- Personas: chips hasta 12 y campo numérico por encima ---

test('cantidadChipsPersonas y usaCampoNumericoPersonas: chips hasta min(máximo, 12); campo numérico si el máximo es mayor', () => {
  assert.equal(MAXIMO_CHIPS_PERSONAS, 12);
  assert.equal(cantidadChipsPersonas({ ...POLITICAS_POR_DEFECTO, maxPersonas: 4 }), 4);
  assert.equal(cantidadChipsPersonas({ ...POLITICAS_POR_DEFECTO, maxPersonas: 12 }), 12);
  assert.equal(cantidadChipsPersonas({ ...POLITICAS_POR_DEFECTO, maxPersonas: 30 }), 12);
  assert.equal(usaCampoNumericoPersonas({ ...POLITICAS_POR_DEFECTO, maxPersonas: 12 }), false);
  assert.equal(usaCampoNumericoPersonas({ ...POLITICAS_POR_DEFECTO, maxPersonas: 13 }), true);
  assert.equal(usaCampoNumericoPersonas(POLITICAS_POR_DEFECTO), false);
});

test('validarPersonasCliente: entero entre 1 y el máximo efectivo', () => {
  assert.deepEqual(validarPersonasCliente('14', 20), { valido: true, valor: 14, error: null });
  assert.deepEqual(validarPersonasCliente(' 1 ', 20), { valido: true, valor: 1, error: null });
  assert.deepEqual(validarPersonasCliente('20', 20), { valido: true, valor: 20, error: null });
  assert.equal(validarPersonasCliente('21', 20).error, 'maximo');
  assert.equal(validarPersonasCliente('0', 20).error, 'minimo');
  for (const texto of ['', 'abc', '2.5', '-3', '1e1']) {
    assert.equal(validarPersonasCliente(texto, 20).error, 'formato', texto);
  }
});

// --- Franjas: todas llegan; las no seleccionables traen su motivo ---

test('estadoFranjaCliente: disponible es seleccionable; no disponible lleva el motivo del servidor', () => {
  assert.deepEqual(estadoFranjaCliente({ id: 'a', disponible: true }), { seleccionable: true, motivo: null });
  assert.deepEqual(
    estadoFranjaCliente({ id: 'b', disponible: false, motivoNoDisponible: 'Esta franja ya pasó.' }),
    { seleccionable: false, motivo: 'Esta franja ya pasó.' },
  );
});

test('estadoFranjaCliente: sin el campo disponible se considera seleccionable; sin motivo usa uno genérico', () => {
  assert.equal(estadoFranjaCliente({ id: 'c' }).seleccionable, true);
  const sinMotivo = estadoFranjaCliente({ id: 'd', disponible: false });
  assert.equal(sinMotivo.seleccionable, false);
  assert.ok(sinMotivo.motivo.length > 0);
});

test('diaCerradoEnFranjas: solo si todas salen deshabilitadas por el restaurante cerrado', () => {
  const cerrada = (id) => ({ id, disponible: false, motivoNoDisponible: MOTIVO_RESTAURANTE_CERRADO });
  assert.equal(diaCerradoEnFranjas([cerrada('a'), cerrada('b')]), true);
  assert.equal(diaCerradoEnFranjas([cerrada('a'), { id: 'b', disponible: true }]), false);
  assert.equal(diaCerradoEnFranjas([cerrada('a'), { id: 'b', disponible: false, motivoNoDisponible: 'Esta franja ya pasó.' }]), false);
  assert.equal(diaCerradoEnFranjas([]), false);
});

test('franjaSigueDisponible: la elegida debe existir en la lista nueva y poder elegirse', () => {
  const lista = [{ id: 'a', disponible: true }, { id: 'b', disponible: false, motivoNoDisponible: 'x' }];
  assert.equal(franjaSigueDisponible({ id: 'a' }, lista), true);
  assert.equal(franjaSigueDisponible({ id: 'b' }, lista), false);
  assert.equal(franjaSigueDisponible({ id: 'z' }, lista), false);
  assert.equal(franjaSigueDisponible(null, lista), false);
});
