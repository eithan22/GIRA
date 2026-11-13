import { useCallback, useEffect, useState } from 'react';
import { hoyEnSantoDomingo } from '../reservasPersonalFormato.js';
import { esPeticionCancelada, mensajeDeError, reservasPersonalService } from '../reservasPersonalService.js';

/**
 * Visitas sin reserva de HOY (GET /api/visitas?fecha=aaaa-mm-dd, GI-402). La fecha va siempre explícita y sale de
 * Santo Domingo con Intl (nunca de la zona del navegador ni del valor por defecto del servidor); se calcula en cada
 * carga, así una pantalla abierta pasada la medianoche pide el día nuevo al recargar.
 * - Carga al montar y cada vez que se llama a `actualizar()` (tras registrar una visita o con «Actualizar»).
 * - Al recargar se conserva lo que ya se mostraba (`recargando`), sin parpadeo; una carga nueva cancela la anterior
 *   (AbortController) y una respuesta vieja nunca pisa a la nueva.
 * - `fase`: 'cargando' (primera vez) | 'listo' | 'error' (con `error` y `reintentar` = `actualizar`).
 * El resultado solo vive en estado de React: sin storage, sin URL, sin consola.
 */
export function useVisitasHoy() {
  const [fase, setFase] = useState('cargando');
  const [recargando, setRecargando] = useState(false);
  const [datos, setDatos] = useState(null);
  const [error, setError] = useState('');
  const [version, setVersion] = useState(0);

  useEffect(() => {
    const controlador = new AbortController();
    setRecargando(true);
    reservasPersonalService.listarVisitas({ fecha: hoyEnSantoDomingo() }, { signal: controlador.signal })
      .then((resultado) => {
        if (controlador.signal.aborted) return;
        setDatos(resultado);
        setError('');
        setFase('listo');
        setRecargando(false);
      })
      .catch((fallo) => {
        if (esPeticionCancelada(fallo) || controlador.signal.aborted) return;
        setError(mensajeDeError(fallo));
        setFase('error');
        setRecargando(false);
      });
    return () => controlador.abort();
  }, [version]);

  const actualizar = useCallback(() => setVersion((v) => v + 1), []);

  return { fase, recargando, datos, error, actualizar };
}
