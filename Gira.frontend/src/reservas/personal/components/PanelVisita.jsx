import { useState } from 'react';
import { TITULO_VISITA } from '../reservasPersonalTextos.js';
import FormularioVisita from './FormularioVisita.jsx';
import PanelPersonal from './PanelPersonal.jsx';

/**
 * Panel que abre el mapa desde una mesa libre: el mismo FormularioVisita de la página
 * /reservas/visitas, con esa mesa ya elegida. Escape y la X no cierran mientras se envía.
 */
export default function PanelVisita({ franjas, mesaInicialId, origenRef, fallbackFocoRef, onCerrar, onRegistrada }) {
  const [enviando, setEnviando] = useState(false);

  return (
    <PanelPersonal
      titulo={TITULO_VISITA}
      tituloId="panel-visita-titulo"
      eyebrow="VISITA SIN RESERVA"
      onCerrar={onCerrar}
      bloqueado={enviando}
      origenRef={origenRef}
      fallbackFocoRef={fallbackFocoRef}
    >
      <FormularioVisita
        franjas={franjas}
        mesaInicialId={mesaInicialId}
        onRegistrada={onRegistrada}
        onCerrar={onCerrar}
        onEnviando={setEnviando}
      />
    </PanelPersonal>
  );
}
