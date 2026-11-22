import { sumarDias } from '../inventario/inventarioFormato.js';

export function diasGrafico(registros, desde, hasta) {
  const inicio = sumarDias(hasta, -13);
  const primeraFecha = desde > inicio ? desde : inicio;
  const totales = new Map();
  for (const registro of registros) {
    totales.set(registro.fecha, (totales.get(registro.fecha) ?? 0) + registro.totalComensales);
  }
  const dias = [];
  for (let fecha = primeraFecha; fecha <= hasta; fecha = sumarDias(fecha, 1)) {
    dias.push({ fecha, total: totales.has(fecha) ? totales.get(fecha) : null });
  }
  return dias;
}

export function validarFechaPrediccion(valor, hoy) {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(valor)) return false;
  const fecha = new Date(`${valor}T00:00:00Z`);
  return !Number.isNaN(fecha.valueOf()) && fecha.toISOString().slice(0, 10) === valor
    && valor >= sumarDias(hoy, -365) && valor <= sumarDias(hoy, 14);
}

export function compararPredicciones(predicciones, historial) {
  const reales = new Map(historial.map((registro) =>
    [`${registro.fecha}|${registro.franjaHorariaId}`, registro.totalComensales]));
  return predicciones.map((prediccion) => ({
    ...prediccion,
    real: reales.get(`${prediccion.fecha}|${prediccion.franjaHorariaId}`) ?? null,
  }));
}

export function validarPredicciones(datos, fecha) {
  if (!Array.isArray(datos)) throw new Error('Respuesta de predicciones inválida');
  const franjas = new Set();
  for (const item of datos) {
    if (!item || item.fecha !== fecha || typeof item.franjaHorariaId !== 'string'
      || !item.franjaHorariaId || franjas.has(item.franjaHorariaId)
      || typeof item.franja !== 'string' || !item.franja
      || !Number.isInteger(item.afluenciaEstimada) || item.afluenciaEstimada < 0
      || typeof item.bajaConfianza !== 'boolean'
      || typeof item.versionModelo !== 'string' || !item.versionModelo
      || typeof item.generadaEn !== 'string' || Number.isNaN(Date.parse(item.generadaEn))
      || !((item.limiteInferior === null && item.limiteSuperior === null)
        || (Number.isFinite(item.limiteInferior) && Number.isFinite(item.limiteSuperior)
          && item.limiteInferior >= 0 && item.limiteSuperior >= item.limiteInferior))) {
      throw new Error('Respuesta de predicciones inválida');
    }
    franjas.add(item.franjaHorariaId);
  }
  return datos;
}

export function validarKpis(datos, periodo) {
  const campos = ['comensalesAtendidos', 'tasaOcupacion', 'tasaNoShow', 'tasaCancelacion',
    'proporcionWalkIns', 'precisionModelo', 'costoInsumosPorComensal', 'merma', 'horasPersonalPorComensal'];
  if (!datos || datos.desde !== periodo.desde || datos.hasta !== periodo.hasta
    || campos.some((campo) => datos[campo] !== null && !Number.isFinite(datos[campo]))
    || !Number.isInteger(datos.alertasAbiertas) || datos.alertasAbiertas < 0
    || !Array.isArray(datos.afluenciaPromedio)
    || datos.afluenciaPromedio.some((item) => !item || !Number.isInteger(item.diaSemana)
      || item.diaSemana < 0 || item.diaSemana > 6
      || typeof item.franjaHorariaId !== 'string' || typeof item.franja !== 'string'
      || !Number.isFinite(item.promedio) || !Number.isInteger(item.fechasObservadas)
      || item.fechasObservadas < 1)) {
    throw new Error('Respuesta de indicadores inválida');
  }
  return datos;
}

export function validarRecomendaciones(datos) {
  if (!Array.isArray(datos)) throw new Error('Respuesta de recomendaciones inválida');
  const ids = new Set();
  for (const item of datos) {
    if (!item || typeof item.id !== 'string' || !item.id || ids.has(item.id)
      || typeof item.fecha !== 'string' || !/^\d{4}-\d{2}-\d{2}$/.test(item.fecha)
      || typeof item.tipo !== 'string' || !item.tipo
      || typeof item.mensaje !== 'string' || !item.mensaje
      || typeof item.prioridad !== 'string' || !item.prioridad
      || !['Nueva', 'Vista', 'Descartada'].includes(item.estado)
      || typeof item.origen !== 'string' || !item.origen
      || typeof item.fechaGeneracion !== 'string' || Number.isNaN(Date.parse(item.fechaGeneracion))) {
      throw new Error('Respuesta de recomendaciones inválida');
    }
    ids.add(item.id);
  }
  return datos;
}

export function mensajeErrorAnalitica(error, recurso) {
  const estado = error?.response?.status;
  console.error(`Error al consultar ${recurso}`, { estado: estado ?? 'sin respuesta' });
  if (estado === 401) return 'Tu sesión ha vencido. Inicia sesión de nuevo.';
  if (estado === 403) return 'No tienes autorización para consultar esta información.';
  if (estado === 400) return 'La fecha o los parámetros de consulta no son válidos.';
  return `No se pudo consultar ${recurso}. Intenta nuevamente.`;
}
