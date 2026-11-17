import Aviso from '../../components/Aviso.jsx';
import { esConflictoReasignar } from '../reservasPersonalFormato.js';
import { AYUDA_REASIGNAR } from '../reservasPersonalTextos.js';

/**
 * Error del servidor tal cual. Bajo cualquier 409 que diga «Reasígnalas antes» se añade una línea de
 * ayuda aparte; el detail del servidor no se modifica.
 */
export default function ErrorSalon({ texto }) {
  return (
    <div className="reservas-personal-bloque">
      <Aviso tipo="error">{texto}</Aviso>
      {esConflictoReasignar(texto) && <p className="reservas-campo-ayuda">{AYUDA_REASIGNAR}</p>}
    </div>
  );
}
