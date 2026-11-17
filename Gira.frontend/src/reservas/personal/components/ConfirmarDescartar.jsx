import { useEffect, useRef } from 'react';
import {
  CUERPO_DESCARTAR,
  TEXTO_DESCARTAR,
  TEXTO_SEGUIR_EDITANDO,
  TITULO_DESCARTAR,
} from '../reservasPersonalTextos.js';

/**
 * Confirmación en línea para cerrar un panel con borrador. El foco empieza en «Seguir editando»
 * (la opción segura) y vuelve a quien lo llamó al cerrarse el bloque.
 */
export default function ConfirmarDescartar({
  onSeguir, onDescartar, titulo = TITULO_DESCARTAR, cuerpo = CUERPO_DESCARTAR, textoDescartar = TEXTO_DESCARTAR,
}) {
  const seguirRef = useRef(null);

  useEffect(() => {
    seguirRef.current?.focus();
  }, []);

  return (
    <section
      className="reservas-personal-confirmacion reservas-personal-confirmacion--peligro"
      role="alertdialog"
      aria-labelledby="descartar-titulo"
      aria-describedby="descartar-cuerpo"
    >
      <h3 id="descartar-titulo" className="reservas-personal-confirmacion-titulo">{titulo}</h3>
      <p id="descartar-cuerpo" className="reservas-personal-confirmacion-texto">{cuerpo}</p>
      <div className="reservas-personal-confirmacion-botones">
        <button
          type="button"
          ref={seguirRef}
          className="reservas-boton reservas-boton--chico reservas-boton--primario reservas-personal-boton-auto"
          onClick={onSeguir}
        >{TEXTO_SEGUIR_EDITANDO}</button>
        <button
          type="button"
          className="reservas-boton reservas-boton--chico reservas-boton--fantasma-error reservas-personal-boton-auto"
          onClick={onDescartar}
        >{textoDescartar}</button>
      </div>
    </section>
  );
}
