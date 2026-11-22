import { api } from '../security/authService.js';

// Llamadas a /api/cargos y /api/empleados (GI-295, GI-296). Usa la instancia de security,
// que adjunta el token de sesión del personal.
export const personalService = {
  async listarCargos() {
    const response = await api.get('/api/cargos');
    return response.data;
  },

  async crearCargo(datos) {
    const response = await api.post('/api/cargos', datos);
    return response.data;
  },

  async actualizarCargo(id, datos) {
    const response = await api.put(`/api/cargos/${id}`, datos);
    return response.data;
  },

  async listarEmpleados({ soloActivos = false } = {}) {
    const response = await api.get('/api/empleados', { params: { soloActivos } });
    return response.data;
  },

  async crearEmpleado(datos) {
    const response = await api.post('/api/empleados', datos);
    return response.data;
  },

  async actualizarEmpleado(id, datos) {
    const response = await api.put(`/api/empleados/${id}`, datos);
    return response.data;
  },

  async cambiarEstadoEmpleado(id, activo) {
    const response = await api.patch(`/api/empleados/${id}/estado`, { activo });
    return response.data;
  },

  // Requerimiento de personal por cargo (GI-302, GI-366): GET y PUT con el cargo en el cuerpo.
  async listarRequerimientos() {
    const response = await api.get('/api/requerimientos-personal');
    return response.data;
  },

  async guardarRequerimiento(datos) {
    const response = await api.put('/api/requerimientos-personal', datos);
    return response.data;
  },

  // Ausencias (GI-305, GI-367). Los enums viajan como números: ver personalFormato.js.
  async listarAusencias({ empleadoId } = {}) {
    const response = await api.get('/api/ausencias', { params: { empleadoId: empleadoId || undefined } });
    return response.data;
  },

  async crearAusencia(datos) {
    const response = await api.post('/api/ausencias', datos);
    return response.data;
  },

  async resolverAusencia(id, estado) {
    const response = await api.patch(`/api/ausencias/${id}/estado`, { estado });
    return response.data;
  },

  // Turnos y planificación (GI-338, GI-301, GI-364). La lista de turnos y la cobertura son rutas distintas.
  async listarTurnos({ desde, hasta, cargoId } = {}) {
    const response = await api.get('/api/turnos', { params: { desde, hasta, cargoId: cargoId || undefined } });
    return response.data;
  },

  async obtenerCobertura({ desde, hasta, cargoId } = {}) {
    const response = await api.get('/api/turnos/cobertura', { params: { desde, hasta, cargoId: cargoId || undefined } });
    return response.data;
  },

  async crearTurno(datos) {
    const response = await api.post('/api/turnos', datos);
    return response.data;
  },

  async actualizarTurno(id, datos) {
    const response = await api.put(`/api/turnos/${id}`, datos);
    return response.data;
  },

  async cambiarEstadoTurno(id, estado) {
    const response = await api.patch(`/api/turnos/${id}/estado`, { estado });
    return response.data;
  },

  // Sugerencias de personal (GI-304, GI-365). Solo cambian el estado de la sugerencia, nunca los turnos.
  async listarSugerencias({ estado } = {}) {
    const response = await api.get('/api/turnos/sugerencias', { params: { estado: estado ?? undefined } });
    return response.data;
  },

  async resolverSugerencia(id, estado) {
    const response = await api.patch(`/api/turnos/sugerencias/${id}`, { estado });
    return response.data;
  },
};
