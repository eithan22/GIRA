import { api } from '../security/authService.js';

export const analiticaService = {
  historial: (periodo) => api.get('/api/analitica/historial', { params: periodo }).then((respuesta) => respuesta.data),
  kpis: (periodo) => api.get('/api/analitica/kpis', { params: periodo }).then((respuesta) => respuesta.data),
  prediccion: (periodo, franjaId) => api.get('/api/analitica/prediccion', {
    params: franjaId ? { ...periodo, franjaId } : periodo,
  }).then((respuesta) => respuesta.data),
  recomendaciones: () => api.get('/api/analitica/recomendaciones').then((respuesta) => respuesta.data),
  actualizarRecomendacion: (id, estado) => api.patch(`/api/analitica/recomendaciones/${id}`, { estado }),
  modelos: () => api.get('/api/analitica/modelos').then((respuesta) => respuesta.data),
  solicitarEntrenamiento: () => api.post('/api/analitica/modelos/entrenamientos').then((respuesta) => respuesta.data),
  entrenamiento: (id) => api.get(`/api/analitica/modelos/entrenamientos/${id}`).then((respuesta) => respuesta.data),
  activarModelo: (id, motivo) => api.post(`/api/analitica/modelos/${id}/activar`, { motivo }),
  feriados: () => api.get('/api/feriados').then((respuesta) => respuesta.data),
  crearFeriado: (feriado) => api.post('/api/feriados', feriado).then((respuesta) => respuesta.data),
  eliminarFeriado: (id) => api.delete(`/api/feriados/${id}`),
};
