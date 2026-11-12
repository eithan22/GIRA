import Aviso from '../../components/Aviso.jsx';
import { TEXTO_CARGANDO, TEXTO_REINTENTAR } from '../reservasPersonalTextos.js';

/**
 * Estados comunes de una pantalla del personal: carga, vacío y error.
 * `estado`: 'cargando' | 'vacio' | 'error'. Reutiliza el Aviso de Eithan y el
 * indicador de carga de su CSS (reservas-cargando-*).
 */
export default function EstadoPagina({ estado, mensaje, onReintentar }) {
  if (estado === 'cargando') {
    return (
      <div className="reservas-personal-estado" role="status">
        <span className="reservas-cargando">
          <span className="reservas-cargando-puntos" aria-hidden="true">
            <span className="reservas-cargando-punto" />
            <span className="reservas-cargando-punto" />
            <span className="reservas-cargando-punto" />
          </span>
          {mensaje || TEXTO_CARGANDO}
        </span>
      </div>
    );
  }

  if (estado === 'error') {
    return (
      <div className="reservas-personal-estado">
        <Aviso tipo="error">{mensaje}</Aviso>
        {onReintentar && (
          <button
            type="button"
            className="reservas-boton reservas-boton--chico reservas-boton--secundario"
            onClick={onReintentar}
          >
            {TEXTO_REINTENTAR}
          </button>
        )}
      </div>
    );
  }

  return (
    <div className="reservas-personal-estado">
      <Aviso tipo="neutro">{mensaje}</Aviso>
    </div>
  );
}
