import { useState } from 'react';
import { TEXTO_CANCELAR, TEXTO_GUARDANDO, TEXTO_GUARDAR } from '../reservasPersonalTextos.js';
import ConfirmarDescartar from './ConfirmarDescartar.jsx';
import ErrorSalon from './ErrorSalon.jsx';
import PanelPersonal from './PanelPersonal.jsx';

/**
 * Carcasa común de los paneles de crear y editar (zona, mesa, franja): PanelPersonal, formulario con
 * un solo botón de guardar (deshabilitado mientras se envía) y confirmación al cerrar si hay cambios
 * sin guardar. El error del servidor se muestra tal cual, con la ayuda del 409 de reasignar.
 */
export default function PanelFormularioSalon({
  titulo, eyebrow, borrador, enviando, error, onSubmit, onCerrar, origenRef, fallbackFocoRef, children,
}) {
  const [descartando, setDescartando] = useState(false);

  function intentarCerrar() {
    if (descartando) {
      setDescartando(false);
      return;
    }
    if (borrador && !enviando) setDescartando(true);
    else onCerrar();
  }

  return (
    <PanelPersonal
      titulo={titulo}
      tituloId="panel-salon-titulo"
      eyebrow={eyebrow}
      onCerrar={intentarCerrar}
      bloqueado={enviando}
      origenRef={origenRef}
      fallbackFocoRef={fallbackFocoRef}
    >
      {descartando && <ConfirmarDescartar onSeguir={() => setDescartando(false)} onDescartar={onCerrar} />}
      <form className="reservas-personal-formulario" onSubmit={onSubmit} noValidate>
        {children}
        {error && <ErrorSalon texto={error} />}
        <div className="reservas-personal-confirmacion-botones">
          <button
            type="submit"
            className="reservas-boton reservas-boton--chico reservas-boton--primario reservas-personal-boton-auto"
            disabled={enviando}
          >{enviando ? TEXTO_GUARDANDO : TEXTO_GUARDAR}</button>
          <button
            type="button"
            className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
            onClick={intentarCerrar}
            disabled={enviando}
          >{TEXTO_CANCELAR}</button>
        </div>
      </form>
    </PanelPersonal>
  );
}
