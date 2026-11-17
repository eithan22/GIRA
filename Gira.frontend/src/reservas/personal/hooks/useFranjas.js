import { useCallback, useEffect, useState } from 'react';
import { esPeticionCancelada, mensajeDeError, reservasPersonalService } from '../reservasPersonalService.js';

/**
 * Franjas operativas (GET /api/reservas/franjas): todas las no borradas, con su bandera `activa`.
 * `estado`: 'cargando' | 'listo' | 'error'. Cada pantalla decide qué hacer con las inactivas.
 */
export function useFranjas() {
  const [franjas, setFranjas] = useState([]);
  const [estado, setEstado] = useState('cargando');
  const [error, setError] = useState('');
  const [intento, setIntento] = useState(0);

  useEffect(() => {
    const controlador = new AbortController();
    setEstado('cargando');
    reservasPersonalService.listarFranjasOperativas({ signal: controlador.signal })
      .then((lista) => {
        setFranjas(Array.isArray(lista) ? lista : []);
        setError('');
        setEstado('listo');
      })
      .catch((fallo) => {
        if (esPeticionCancelada(fallo)) return;
        setError(mensajeDeError(fallo));
        setEstado('error');
      });
    return () => controlador.abort();
  }, [intento]);

  const reintentar = useCallback(() => setIntento((n) => n + 1), []);

  return { franjas, estado, error, reintentar };
}
