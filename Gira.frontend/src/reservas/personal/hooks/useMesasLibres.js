import { useCallback, useEffect, useRef, useState } from 'react';
import { esFechaIso, horaActualSantoDomingo } from '../reservasPersonalFormato.js';
import { esPeticionCancelada, mensajeDeError, reservasPersonalService } from '../reservasPersonalService.js';

const DEMORA_MS = 350;
const INTERVALO_AUTOMATICO_MS = 60000;

/**
 * Mapa de ocupación (GET /api/reservas/ocupacion) de una fecha y franja: sirve al mapa de mesas, a
 * las mesas libres de «Modificar» y a las de «Visitas». Espera 350 ms desde el último cambio y cancela
 * la consulta anterior. Un error no bloquea nada: devuelve el detail y `reintentar()` (acción del
 * usuario, sin demora).
 *
 * Refresco automático opcional (`autoRefresco`): cada 60 s, solo con la pestaña visible, en pausa
 * mientras `pausado` (por ejemplo, con un panel abierto), y se detiene ante un 401 o 403 (`detenido`)
 * hasta que el usuario reintente a mano.
 *
 * `estado`: 'idle' | 'cargando' | 'listo' | 'error'. `actualizadoA`: "HH:mm" de la última carga buena.
 */
export function useMesasLibres({ fecha, franjaId, autoRefresco = false, pausado = false }) {
  const [estado, setEstado] = useState('idle');
  const [mapa, setMapa] = useState(null);
  const [error, setError] = useState('');
  const [actualizadoA, setActualizadoA] = useState('');
  const [detenido, setDetenido] = useState(false);
  const [intento, setIntento] = useState(0);
  const sinDemoraRef = useRef(false);

  const habilitado = esFechaIso(fecha) && Boolean(franjaId);

  useEffect(() => {
    if (!habilitado) {
      setEstado('idle');
      setMapa(null);
      setError('');
      return undefined;
    }

    const controlador = new AbortController();
    setEstado('cargando');
    setError('');
    const demora = sinDemoraRef.current ? 0 : DEMORA_MS;
    sinDemoraRef.current = false;

    const temporizador = setTimeout(() => {
      reservasPersonalService.obtenerOcupacion({ fecha, franjaId }, { signal: controlador.signal })
        .then((resultado) => {
          setMapa(resultado);
          setActualizadoA(horaActualSantoDomingo());
          setEstado('listo');
        })
        .catch((fallo) => {
          if (esPeticionCancelada(fallo)) return;
          const estadoHttp = fallo?.response?.status;
          if (estadoHttp === 401 || estadoHttp === 403) setDetenido(true);
          setMapa(null);
          setError(mensajeDeError(fallo));
          setEstado('error');
        });
    }, demora);

    return () => {
      clearTimeout(temporizador);
      controlador.abort();
    };
  }, [fecha, franjaId, habilitado, intento]);

  const reintentar = useCallback(() => {
    sinDemoraRef.current = true;
    setDetenido(false);
    setIntento((n) => n + 1);
  }, []);

  // Refresco automático: solo si está activado, hay mapa que refrescar, no hay pausa ni detención.
  useEffect(() => {
    if (!autoRefresco || pausado || detenido || !habilitado) return undefined;
    const id = setInterval(() => {
      if (document.visibilityState === 'visible') {
        sinDemoraRef.current = true;
        setIntento((n) => n + 1);
      }
    }, INTERVALO_AUTOMATICO_MS);
    return () => clearInterval(id);
  }, [autoRefresco, pausado, detenido, habilitado]);

  return { estado, mapa, error, actualizadoA, detenido, reintentar };
}
