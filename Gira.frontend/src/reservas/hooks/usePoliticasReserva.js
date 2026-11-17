import { useEffect, useState } from 'react';
import { reservasService } from '../reservasService.js';
import { POLITICAS_POR_DEFECTO, normalizarPoliticas } from '../reservasFormato.js';

const VIGENCIA_MS = 60_000;

// Una sola petición compartida por todos los componentes de /reservar, y una vigencia corta: la ruta pública
// tiene límite por IP y las políticas cambian pocas veces.
let guardadas = null; // { politicas, momento }
let enCurso = null;

function cargar() {
  if (guardadas && Date.now() - guardadas.momento < VIGENCIA_MS) return Promise.resolve(guardadas.politicas);
  if (!enCurso) {
    enCurso = reservasService.obtenerPoliticas()
      .then((datos) => {
        const politicas = normalizarPoliticas(datos);
        guardadas = { politicas, momento: Date.now() };
        return politicas;
      })
      .catch(() => null) // si falla se siguen usando los valores por defecto; nunca bloquea la pantalla
      .finally(() => { enCurso = null; });
  }
  return enCurso;
}

/**
 * Políticas de reserva para los textos y límites de /reservar (GI-226). Mientras cargan (o si la petición falla)
 * devuelve POLITICAS_POR_DEFECTO; `cargadas` indica si ya son las del servidor.
 */
export function usePoliticasReserva() {
  const [estado, setEstado] = useState(() => ({
    politicas: guardadas?.politicas ?? POLITICAS_POR_DEFECTO,
    cargadas: Boolean(guardadas),
  }));

  useEffect(() => {
    let activo = true;
    cargar().then((politicas) => {
      if (activo && politicas) setEstado({ politicas, cargadas: true });
    });
    return () => { activo = false; };
  }, []);

  return estado;
}
