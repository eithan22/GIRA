import { useEffect, useState } from 'react';
import { esPeticionCancelada, reservasPersonalService } from '../reservasPersonalService.js';
import { estadoRestauranteAhora } from '../reservasPersonalFormato.js';

/**
 * Si el restaurante está abierto AHORA, para avisar antes de intentar registrar una visita sin reserva (el
 * servidor tampoco la acepta fuera del horario). Lee el horario por día y los días cerrados una vez, y recalcula
 * con `hoy` y `hora` (useAhora). Es solo informativo: si la carga falla (`estado: 'error'`) no se bloquea nada y
 * manda el 409 del servidor, que se muestra tal cual.
 *
 * `estado`: 'cargando' | 'listo' | 'error'. `restaurante`: { estado: 'abierto' | 'cerradoHoy' | 'cerradoAhora',
 * apertura, cierre } o null mientras no haya datos.
 */
export function useRestauranteAbierto({ hoy, hora }) {
  const [estado, setEstado] = useState('cargando');
  const [datos, setDatos] = useState(null);

  useEffect(() => {
    const controlador = new AbortController();
    Promise.all([
      reservasPersonalService.listarHorario({ signal: controlador.signal }),
      reservasPersonalService.listarDiasCerrados({ signal: controlador.signal }),
    ])
      .then(([horarios, diasCerrados]) => {
        setDatos({ horarios, diasCerrados });
        setEstado('listo');
      })
      .catch((fallo) => {
        if (esPeticionCancelada(fallo)) return;
        setEstado('error');
      });
    return () => controlador.abort();
  }, []);

  const restaurante = datos
    ? estadoRestauranteAhora({
      horarios: datos.horarios,
      diasCerrados: Array.isArray(datos.diasCerrados) ? datos.diasCerrados : (datos.diasCerrados?.items ?? []),
      hoy,
      hora,
    })
    : null;

  return { estado, restaurante };
}
