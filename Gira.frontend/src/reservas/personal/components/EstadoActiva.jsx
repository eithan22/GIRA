import { TEXTO_ACTIVA, TEXTO_INACTIVA } from '../reservasPersonalTextos.js';

/**
 * Estado activa/inactiva con símbolo y texto (nunca solo color): ● Activa / ○ Inactiva.
 * `extra` permite añadir una etiqueta más, por ejemplo «Zona inactiva» en una mesa activa.
 */
export default function EstadoActiva({ activa, extra }) {
  return (
    <span className="reservas-personal-estado-activa">
      <span className={`reservas-personal-pill-activa${activa ? '' : ' reservas-personal-pill-activa--inactiva'}`}>
        <span aria-hidden="true">{activa ? '●' : '○'}</span> {activa ? TEXTO_ACTIVA : TEXTO_INACTIVA}
      </span>
      {extra && <span className="reservas-personal-etiqueta-zona-inactiva">{extra}</span>}
    </span>
  );
}
