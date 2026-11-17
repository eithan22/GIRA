import { useCallback, useEffect, useState } from 'react';
import { TAMANO_LISTA_SELECTORES, listaIncompleta } from '../reservasPersonalFormato.js';
import { esPeticionCancelada, mensajeDeError, reservasPersonalService } from '../reservasPersonalService.js';

/**
 * Zonas para selectores y filtros (GET /api/zonas con pageSize 100, el máximo del backend). Si hay más
 * de 100 (`truncado`), la pantalla lo avisa en vez de recortar en silencio.
 * `estado`: 'cargando' | 'listo' | 'error'.
 */
export function useZonasSalon() {
  const [zonas, setZonas] = useState([]);
  const [truncado, setTruncado] = useState(false);
  const [estado, setEstado] = useState('cargando');
  const [error, setError] = useState('');
  const [version, setVersion] = useState(0);

  useEffect(() => {
    const controlador = new AbortController();
    setEstado('cargando');
    reservasPersonalService.listarZonas({ page: 1, pageSize: TAMANO_LISTA_SELECTORES }, { signal: controlador.signal })
      .then((pagina) => {
        setZonas(Array.isArray(pagina?.items) ? pagina.items : []);
        setTruncado(listaIncompleta(pagina));
        setError('');
        setEstado('listo');
      })
      .catch((fallo) => {
        if (esPeticionCancelada(fallo)) return;
        setError(mensajeDeError(fallo));
        setEstado('error');
      });
    return () => controlador.abort();
  }, [version]);

  const recargar = useCallback(() => setVersion((n) => n + 1), []);

  return { zonas, truncado, estado, error, recargar };
}
