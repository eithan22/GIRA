import { useState } from 'react';
import { TITULO_VISITA } from '../reservasPersonalTextos.js';
import FormularioVisita from './FormularioVisita.jsx';
import PanelPersonal from './PanelPersonal.jsx';

/**
 * Panel de «Visita sin reserva», único para la Agenda y el Mapa: el Mapa lo abre desde una mesa libre (con esa mesa ya
 * elegida, `mesaInicialId`) o desde el botón general (sin mesa elegida); la Agenda, desde su botón. Escape y la X no
 * cierran mientras se envía.
 */
export default function PanelVisita({
  franjas, franjasEstado, franjasError, onReintentarFranjas,
  mesaInicialId, origenRef, fallbackFocoRef, onCerrar, onRegistrada,
}) {
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
        franjasEstado={franjasEstado}
        franjasError={franjasError}
        onReintentarFranjas={onReintentarFranjas}
        mesaInicialId={mesaInicialId}
        onRegistrada={onRegistrada}
        onCerrar={onCerrar}
        onEnviando={setEnviando}
      />
    </PanelPersonal>
  );
}
