import { useRef } from 'react';
import {
  ETIQUETA_SECCIONES_CONFIGURACION,
  TEXTO_PESTANA_DIAS_CERRADOS,
  TEXTO_PESTANA_FRANJAS,
  TEXTO_PESTANA_HORARIO,
  TEXTO_PESTANA_MESAS,
  TEXTO_PESTANA_POLITICAS,
  TEXTO_PESTANA_ZONAS,
} from '../reservasPersonalTextos.js';

const PESTANAS = [
  ['zonas', TEXTO_PESTANA_ZONAS],
  ['mesas', TEXTO_PESTANA_MESAS],
  ['franjas', TEXTO_PESTANA_FRANJAS],
  ['politicas', TEXTO_PESTANA_POLITICAS],
  ['horario', TEXTO_PESTANA_HORARIO],
  ['dias-cerrados', TEXTO_PESTANA_DIAS_CERRADOS],
];

/**
 * Pestañas internas de Configuración con el patrón ARIA de pestañas (tablist/tab, flechas, Inicio y
 * Fin, tabindex móvil) y aspecto de control segmentado, distinto de las pestañas de navegación de
 * Reservas (que son enlaces). El panel que controla cada una es `panel-<clave>`.
 */
export default function PestanasConfiguracion({ activa, onCambiar }) {
  const refs = useRef({});

  function alTeclear(evento, indice) {
    let destino;
    if (evento.key === 'ArrowRight') destino = (indice + 1) % PESTANAS.length;
    else if (evento.key === 'ArrowLeft') destino = (indice - 1 + PESTANAS.length) % PESTANAS.length;
    else if (evento.key === 'Home') destino = 0;
    else if (evento.key === 'End') destino = PESTANAS.length - 1;
    if (destino === undefined) return;
    evento.preventDefault();
    const [clave] = PESTANAS[destino];
    onCambiar(clave);
    refs.current[clave]?.focus();
  }

  return (
    <div role="tablist" aria-label={ETIQUETA_SECCIONES_CONFIGURACION} className="reservas-personal-segmentado">
      {PESTANAS.map(([clave, etiqueta], indice) => (
        <button
          key={clave}
          ref={(elemento) => { refs.current[clave] = elemento; }}
          type="button"
          role="tab"
          id={`pestana-${clave}`}
          aria-selected={activa === clave}
          aria-controls={`panel-${clave}`}
          tabIndex={activa === clave ? 0 : -1}
          className={`reservas-personal-segmento${activa === clave ? ' reservas-personal-segmento--activo' : ''}`}
          onClick={() => onCambiar(clave)}
          onKeyDown={(evento) => alTeclear(evento, indice)}
        >{etiqueta}</button>
      ))}
    </div>
  );
}
