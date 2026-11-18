import { api } from '../security/authService.js';
import { limpiar } from './inventarioFormato.js';

// Usa el `api` de Seguridad (token + renovación). El 401 ya lo resuelve su interceptor.
const datos = (promesa) => promesa.then((r) => r.data);

export const inventarioService = {
  listarInsumos: (filtros) => datos(api.get('/api/insumos', { params: limpiar(filtros) })),
  resumenInsumos: () => datos(api.get('/api/insumos/resumen')),
  crearInsumo: (cuerpo) => datos(api.post('/api/insumos', cuerpo)),
  actualizarInsumo: (id, cuerpo) => datos(api.put(`/api/insumos/${id}`, cuerpo)),
  cambiarEstadoInsumo: (id, activo) => datos(api.patch(`/api/insumos/${id}/estado`, { activo })),

  listarPlatos: () => datos(api.get('/api/platos')),
  crearPlato: (cuerpo) => datos(api.post('/api/platos', cuerpo)),
  actualizarPlato: (id, cuerpo) => datos(api.put(`/api/platos/${id}`, cuerpo)),
  obtenerReceta: (platoId) => datos(api.get(`/api/platos/${platoId}/receta`)),
  guardarReceta: (platoId, cuerpo) => datos(api.put(`/api/platos/${platoId}/receta`, cuerpo)),

  registrarMovimiento: (cuerpo) => datos(api.post('/api/movimientos', cuerpo)),
  kardex: (filtros) => datos(api.get('/api/movimientos', { params: limpiar(filtros) })),
  conteoFisico: (cuerpo) => datos(api.post('/api/movimientos/conteo-fisico', cuerpo)),

  listarAlertas: (filtros) => datos(api.get('/api/inventario/alertas', { params: limpiar(filtros) })),
  cambiarEstadoAlerta: (id, estado) => datos(api.patch(`/api/inventario/alertas/${id}`, { estado })),
  proyeccion: (filtros) => datos(api.get('/api/inventario/consumo-proyectado', { params: limpiar(filtros) })),

  // Catálogos: `recurso` es 'unidades-medida', 'categorias-insumo', 'categorias-plato' o 'proveedores'.
  listarCatalogo: (recurso) => datos(api.get(`/api/${recurso}`)),
  crearCatalogo: (recurso, cuerpo) => datos(api.post(`/api/${recurso}`, cuerpo)),
  actualizarCatalogo: (recurso, id, cuerpo) => datos(api.put(`/api/${recurso}/${id}`, cuerpo)),
};

/** Todos los insumos activos (para listas desplegables). */
export async function todosLosInsumos() {
  const pagina = await inventarioService.listarInsumos({ page: 1, pageSize: 200 });
  return pagina.items;
}
