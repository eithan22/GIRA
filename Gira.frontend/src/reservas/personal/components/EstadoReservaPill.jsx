import { ESTADO_DESCONOCIDO } from '../../reservasTextos.js';
import { ESTADOS_RESERVA_PERSONAL } from '../reservasPersonalTextos.js';

/**
 * Pastilla de estado con las etiquetas del personal (ESTADOS_RESERVA_PERSONAL). Reutiliza
 * .reservas-estado-pill y .reservas-estado-punto de Eithan; el color va en línea, igual que allí.
 * Lleva texto además de color: el estado nunca depende solo del color.
 */
export default function EstadoReservaPill({ estado }) {
  const info = ESTADOS_RESERVA_PERSONAL[estado] ?? ESTADO_DESCONOCIDO;
  const etiqueta = ESTADOS_RESERVA_PERSONAL[estado]?.etiqueta ?? estado;
  return (
    <span
      className="reservas-estado-pill reservas-personal-pill"
      style={{ background: info.fondo, color: info.texto, '--reservas-punto-color': info.punto }}
    >
      <span className="reservas-estado-punto" aria-hidden="true" />
      {etiqueta}
    </span>
  );
}
