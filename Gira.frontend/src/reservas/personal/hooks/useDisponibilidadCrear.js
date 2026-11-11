import { useCallback, useEffect, useRef, useState } from 'react';
import { fechaEnRango, validarPersonas } from '../reservasPersonalFormato.js';
import { esPeticionCancelada, mensajeDeError, reservasPersonalService } from '../reservasPersonalService.js';

const DEMORA_MS = 350;

/**
 * Disponibilidad de franjas para crear una reserva (GET /api/reservas/disponibilidad).
 * - Las consultas automáticas esperan 350 ms desde el último cambio de fecha o personas y cancelan
 *   la anterior (AbortController); no hay ninguna otra consulta automática.
 * - Un error (incluido el 429 del límite por IP) NO bloquea el formulario: se devuelve el detail del
 *   servidor y `reintentar()`, que es una acción del usuario y consulta sin demora.
 * `estado`: 'idle' (faltan fecha o personas válidas) | 'cargando' | 'listo' | 'error'.
 */
export function useDisponibilidadCrear({ fecha, personas, hoy }) {
  const [estado, setEstado] = useState('idle');
  const [disponibles, setDisponibles] = useState(null);
  const [error, setError] = useState('');
  const [intento, setIntento] = useState(0);
  const sinDemoraRef = useRef(false);

  const validacionPersonas = validarPersonas(personas);
  const validas = fechaEnRango(fecha, hoy) && validacionPersonas.valido;
  const cantidad = validas ? validacionPersonas.valor : null;

  useEffect(() => {
    if (!validas) {
      setEstado('idle');
      setDisponibles(null);
      setError('');
      return undefined;
    }

    const controlador = new AbortController();
    setEstado('cargando');
    setError('');
    const demora = sinDemoraRef.current ? 0 : DEMORA_MS;
    sinDemoraRef.current = false;

    const temporizador = setTimeout(() => {
      reservasPersonalService.consultarDisponibilidad({ fecha, personas: cantidad }, { signal: controlador.signal })
        .then((lista) => {
          setDisponibles(Array.isArray(lista) ? lista : []);
          setEstado('listo');
        })
        .catch((fallo) => {
          if (esPeticionCancelada(fallo)) return;
          setDisponibles(null);
          setError(mensajeDeError(fallo));
          setEstado('error');
        });
    }, demora);

    return () => {
      clearTimeout(temporizador);
      controlador.abort();
    };
  }, [fecha, cantidad, validas, intento]);

  const reintentar = useCallback(() => {
    sinDemoraRef.current = true;
    setIntento((n) => n + 1);
  }, []);

  return { estado, disponibles, error, reintentar };
}
