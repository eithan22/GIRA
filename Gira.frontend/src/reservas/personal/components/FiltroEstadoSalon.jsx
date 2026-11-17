import { FILTROS_ESTADO_SALON } from '../reservasPersonalTextos.js';

/** Filtro de estado de las listas de configuración: Todas, Activas o Inactivas (parámetro `activa`). */
export default function FiltroEstadoSalon({ id, valor, onCambiar }) {
  return (
    <div className="reservas-campo">
      <label className="reservas-campo-etiqueta" htmlFor={id}>Estado</label>
      <select id={id} className="reservas-input" value={valor} onChange={(evento) => onCambiar(evento.target.value)}>
        {FILTROS_ESTADO_SALON.map(([clave, etiqueta]) => <option key={clave} value={clave}>{etiqueta}</option>)}
      </select>
    </div>
  );
}
