import { useState } from 'react';

/**
 * Error al cargar una pantalla con «Reintentar» (GI-489): `reintento` va en las dependencias del efecto que carga,
 * así que `reintentar()` borra el aviso y vuelve a pedir los datos.
 */
export function useCarga() {
  const [errorCarga, setErrorCarga] = useState('');
  const [reintento, setReintento] = useState(0);
  const reintentar = () => { setErrorCarga(''); setReintento((n) => n + 1); };
  return { errorCarga, setErrorCarga, reintento, reintentar };
}
