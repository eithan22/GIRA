import { useCallback, useEffect, useState } from 'react';
import { TAMANO_PAGINA_SALON, reemplazarPorId } from '../reservasPersonalFormato.js';
import { esPeticionCancelada, mensajeDeError, reservasPersonalService } from '../reservasPersonalService.js';

const LISTADORES = {
  zonas: (filtros, opciones) => reservasPersonalService.listarZonas(filtros, opciones),
  mesas: (filtros, opciones) => reservasPersonalService.listarMesas(filtros, opciones),
  franjas: (filtros, opciones) => reservasPersonalService.listarFranjas(filtros, opciones),
};

/**
 * Lista paginada de zonas, mesas o franjas (GET /api/{recurso}) con filtros `activa` y, en mesas,
 * `zonaId`. Cambiar un filtro o la página cancela la petición anterior (AbortController).
 * `estado`: 'cargando' | 'listo' | 'error'. `recargar()` vuelve a pedir; `reemplazar(dto)` cambia una
 * fila con la respuesta del servidor sin recargar la lista.
 */
export function useListaSalon({ recurso, activa, zonaId, page }) {
  const [items, setItems] = useState([]);
  const [total, setTotal] = useState(0);
  const [estado, setEstado] = useState('cargando');
  const [error, setError] = useState('');
  const [version, setVersion] = useState(0);

  useEffect(() => {
    const controlador = new AbortController();
    setEstado('cargando');
    LISTADORES[recurso]({ page, pageSize: TAMANO_PAGINA_SALON, activa, zonaId }, { signal: controlador.signal })
      .then((pagina) => {
        setItems(Array.isArray(pagina?.items) ? pagina.items : []);
        setTotal(pagina?.total ?? 0);
        setError('');
        setEstado('listo');
      })
      .catch((fallo) => {
        if (esPeticionCancelada(fallo)) return;
        setError(mensajeDeError(fallo));
        setEstado('error');
      });
    return () => controlador.abort();
  }, [recurso, activa, zonaId, page, version]);

  const recargar = useCallback(() => setVersion((n) => n + 1), []);
  const reemplazar = useCallback((dto) => setItems((actuales) => reemplazarPorId(actuales, dto)), []);

  return { items, total, estado, error, recargar, reemplazar };
}
