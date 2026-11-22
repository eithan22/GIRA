import { test } from 'node:test';
import { deepStrictEqual, throws } from 'node:assert';
import { compararPredicciones, diasGrafico, validarFechaPrediccion, validarKpis, validarPredicciones, validarRecomendaciones } from './dashboardDatos.js';

test('distingue fechas sin registros de ceros recibidos y suma las franjas', () => {
  const registros = [
    { fecha: '2026-11-18', totalComensales: 0 },
    { fecha: '2026-11-20', totalComensales: 3 },
    { fecha: '2026-11-20', totalComensales: 2 },
  ];
  deepStrictEqual(diasGrafico(registros, '2026-11-18', '2026-11-20'), [
    { fecha: '2026-11-18', total: 0 },
    { fecha: '2026-11-19', total: null },
    { fecha: '2026-11-20', total: 5 },
  ]);
});

test('valida fechas históricas y futuras sin sustituir la fecha introducida', () => {
  for (const valor of ['2025-11-20', '2026-11-20', '2026-12-04']) {
    deepStrictEqual(validarFechaPrediccion(valor, '2026-11-20'), true);
  }
  for (const valor of ['', '2025-11-19', '2026-02-30', '2026-12-05']) {
    deepStrictEqual(validarFechaPrediccion(valor, '2026-11-20'), false);
  }
});

test('compara por fecha y franja sin cruzar días ni inventar asistencia', () => {
  const predicciones = [
    { fecha: '2026-11-20', franjaHorariaId: 'a', afluenciaEstimada: 10 },
    { fecha: '2026-11-20', franjaHorariaId: 'b', afluenciaEstimada: 0 },
  ];
  deepStrictEqual(compararPredicciones(predicciones, [
    { fecha: '2026-11-19', franjaHorariaId: 'a', totalComensales: 4 },
    { fecha: '2026-11-20', franjaHorariaId: 'b', totalComensales: 0 },
  ]), [
    { fecha: '2026-11-20', franjaHorariaId: 'a', afluenciaEstimada: 10, real: null },
    { fecha: '2026-11-20', franjaHorariaId: 'b', afluenciaEstimada: 0, real: 0 },
  ]);
});

test('rechaza respuestas incompletas, de otra fecha o franjas duplicadas', () => {
  const dato = {
    fecha: '2026-11-21', franjaHorariaId: 'a', franja: 'Cena', afluenciaEstimada: 0,
    limiteInferior: null, limiteSuperior: null, bajaConfianza: true,
    generadaEn: '2026-11-20T12:00:00Z', versionModelo: 'base-1',
  };
  deepStrictEqual(validarPredicciones([dato], dato.fecha), [dato]);
  deepStrictEqual(validarPredicciones([], dato.fecha), []);
  throws(() => validarPredicciones([{ ...dato, bajaConfianza: undefined }], dato.fecha));
  throws(() => validarPredicciones([dato], '2026-11-22'));
  throws(() => validarPredicciones([dato, dato], dato.fecha));
});

test('rechaza KPI incompletos o de otro período sin confundir cero con ausencia', () => {
  const periodo = { desde: '2026-11-01', hasta: '2026-11-20' };
  const datos = {
    ...periodo, comensalesAtendidos: 0, tasaOcupacion: null, tasaNoShow: 0,
    tasaCancelacion: null, proporcionWalkIns: null, precisionModelo: null,
    costoInsumosPorComensal: null, merma: null, horasPersonalPorComensal: null,
    alertasAbiertas: 0, afluenciaPromedio: [],
  };
  deepStrictEqual(validarKpis(datos, periodo), datos);
  throws(() => validarKpis({ ...datos, tasaNoShow: undefined }, periodo));
  throws(() => validarKpis(datos, { ...periodo, hasta: '2026-11-19' }));
});

test('limita la gráfica a los últimos catorce días del período', () => {
  const dias = diasGrafico([], '2026-10-01', '2026-11-20');
  deepStrictEqual([dias.length, dias[0].fecha, dias.at(-1).fecha], [14, '2026-11-07', '2026-11-20']);
});

test('valida recomendaciones y rechaza estados o respuestas incompletas', () => {
  const item = { id: 'a', fecha: '2026-11-20', tipo: 'Analítica', mensaje: 'Faltan datos',
    prioridad: 'Media', estado: 'Nueva', origen: 'Predicción', fechaGeneracion: '2026-11-20T12:00:00Z' };
  deepStrictEqual(validarRecomendaciones([item]), [item]);
  deepStrictEqual(validarRecomendaciones([]), []);
  throws(() => validarRecomendaciones([{ ...item, estado: 'otro' }]));
  throws(() => validarRecomendaciones([item, item]));
  throws(() => validarRecomendaciones([{ ...item, mensaje: undefined }]));
});
