import { useEffect, useState } from 'react';
import { esPeticionCancelada, reservasPersonalService } from '../reservasPersonalService.js';

const DEMORA_MS = 300;

/**
 * Aviso (no bloqueante) de próxima reserva al elegir mesa y franja para una visita sin reserva (GI-4xx):
 * si esa mesa tiene una reserva vigente en una franja posterior del mismo día. Nunca bloquea el registro,
 * ni siquiera si la consulta falla — un fallo solo deja de mostrar el aviso (`estado: 'error'`), el
 * formulario sigue permitiendo registrar con normalidad.
 *
 * `estado`: 'idle' (sin mesa elegida, nada que consultar) | 'cargando' | 'listo' | 'error'.
 */
export function useAdvertenciaMesa({ mesaId, franjaId, fecha }) {
  const [estado, setEstado] = useState('idle');
  const [advertencia, setAdvertencia] = useState(null);

  const habilitado = Boolean(mesaId) && Boolean(franjaId) && Boolean(fecha);

  useEffect(() => {
    if (!habilitado) {
      setEstado('idle');
      setAdvertencia(null);
      return undefined;
    }

    const controlador = new AbortController();
    setEstado('cargando');

    const temporizador = setTimeout(() => {
      reservasPersonalService.obtenerAdvertenciaMesa({ mesaId, franjaId, fecha }, { signal: controlador.signal })
        .then((resultado) => {
          setAdvertencia(resultado);
          setEstado('listo');
        })
        .catch((fallo) => {
          if (esPeticionCancelada(fallo)) return;
          setAdvertencia(null);
          setEstado('error');
        });
    }, DEMORA_MS);

    return () => {
      clearTimeout(temporizador);
      controlador.abort();
    };
  }, [mesaId, franjaId, fecha, habilitado]);

  return { estado, advertencia };
}
