import { useEffect, useState } from 'react';
import { contextoSantoDomingo } from '../reservasPersonalFormato.js';

/**
 * Fecha y hora actuales en Santo Domingo ({ hoy, hora }), recalculadas cada minuto para que «franja
 * actual» y «ya empezó» no queden desfasadas en una pantalla abierta mucho rato. Siempre con Intl y
 * America/Santo_Domingo, nunca con la zona del navegador.
 */
export function useAhora(intervaloMs = 60000) {
  const [contexto, setContexto] = useState(() => contextoSantoDomingo());

  useEffect(() => {
    const id = setInterval(() => setContexto(contextoSantoDomingo()), intervaloMs);
    return () => clearInterval(id);
  }, [intervaloMs]);

  return contexto;
}
