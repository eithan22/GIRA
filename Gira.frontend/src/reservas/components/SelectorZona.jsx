import { opcionesSelectorZona } from '../zonasReserva.js';
import { AYUDA_SELECTOR_ZONA, TITULO_SELECTOR_ZONA } from '../reservasTextos.js';

/**
 * Selector de zona del Paso 1 (GI-4xx): un grupo de radios nativos (fieldset + legend) con "Sin preferencia"
 * por defecto y una opción por zona con mesa libre en la franja elegida. No se renderiza si no hay una decisión
 * real que tomar (una sola zona disponible). `zonaId` vacío o null = sin preferencia.
 */
export default function SelectorZona({ franja, zonaId, onElegir }) {
  const opciones = opcionesSelectorZona(franja);
  if (opciones.length === 0) return null;
  const valorActual = zonaId || '';

  return (
    <fieldset className="reservas-zonas">
      <legend className="reservas-seccion-titulo reservas-zonas-titulo">{TITULO_SELECTOR_ZONA}</legend>
      <div className="reservas-zonas-opciones">
        {opciones.map((opcion) => {
          const id = `reservas-zona-${opcion.valor || 'sin-preferencia'}`;
          const activa = opcion.valor === valorActual;
          return (
            <span key={opcion.valor || 'sin-preferencia'} className="reservas-zona">
              <input
                id={id}
                type="radio"
                name="reservas-zona"
                className="reservas-zona-radio"
                value={opcion.valor}
                checked={activa}
                onChange={() => onElegir(opcion.valor || null)}
              />
              <label htmlFor={id} className={`reservas-zona-etiqueta${activa ? ' reservas-zona-etiqueta--activa' : ''}`}>
                {opcion.etiqueta}
              </label>
            </span>
          );
        })}
      </div>
      <p className="reservas-nota reservas-zonas-ayuda">{AYUDA_SELECTOR_ZONA}</p>
    </fieldset>
  );
}
