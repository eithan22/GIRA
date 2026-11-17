import { useEffect, useState } from 'react';
import { esPeticionCancelada, mensajeDeError } from '../reservasPersonalService.js';

/**
 * Carga una vez (y al reintentar) un dato de Configuración. `cargar({ signal })` devuelve la promesa del servicio.
 * `estado`: 'cargando' | 'listo' | 'error'.
 */
export function useCargaConfiguracion(cargar) {
  const [estado, setEstado] = useState('cargando');
  const [datos, setDatos] = useState(null);
  const [error, setError] = useState('');
  const [intento, setIntento] = useState(0);

  useEffect(() => {
    const controlador = new AbortController();
    setEstado('cargando');
    cargar({ signal: controlador.signal })
      .then((resultado) => {
        setDatos(resultado);
        setError('');
        setEstado('listo');
      })
      .catch((fallo) => {
        if (esPeticionCancelada(fallo)) return;
        setError(mensajeDeError(fallo));
        setEstado('error');
      });
    return () => controlador.abort();
    // `cargar` es una función del servicio, estable; solo se vuelve a pedir al reintentar.
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [intento]);

  return { estado, datos, setDatos, error, recargar: () => setIntento((n) => n + 1) };
}
