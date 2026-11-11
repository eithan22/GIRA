import Aviso from '../../components/Aviso.jsx';
import { formatearHora } from '../../reservasFormato.js';
import {
  TEXTO_ACTUALIZAR_DISPONIBILIDAD,
  TEXTO_CONSULTANDO_DISPONIBILIDAD,
  TEXTO_ELIGE_FECHA_Y_PERSONAS,
  TEXTO_FRANJA_DISPONIBLE,
  TEXTO_FRANJA_SIN_CONFIRMAR,
  TEXTO_FRANJA_SIN_DISPONIBILIDAD,
  TEXTO_REINTENTAR,
  TEXTO_SIN_DISPONIBILIDAD_PERSONAL,
  TEXTO_SIN_FRANJAS_ACTIVAS,
  textoFranjasDisponibles,
} from '../reservasPersonalTextos.js';

function textoEstado(franja) {
  if (franja.disponible === true) return TEXTO_FRANJA_DISPONIBLE;
  if (franja.disponible === false) return TEXTO_FRANJA_SIN_DISPONIBILIDAD;
  return TEXTO_FRANJA_SIN_CONFIRMAR;
}

/**
 * Franjas con su disponibilidad real (GET /api/reservas/disponibilidad). Las que no vuelven en esa
 * lista se muestran deshabilitadas con el motivo. Si la consulta falla (por ejemplo un 429), el
 * detail del servidor se muestra, se puede reintentar y las franjas siguen siendo elegibles
 * («Disponibilidad sin confirmar»): el formulario nunca se bloquea.
 */
export default function SelectorFranjas({
  franjas, valor, onCambiar, estado, error, onReintentar, mensajeCampo, legendRef,
}) {
  const disponibles = franjas.filter((franja) => franja.disponible === true).length;
  const mostrarOpciones = estado === 'listo' || estado === 'error';

  let mensajeEstado = '';
  if (estado === 'idle') mensajeEstado = TEXTO_ELIGE_FECHA_Y_PERSONAS;
  else if (estado === 'cargando') mensajeEstado = TEXTO_CONSULTANDO_DISPONIBILIDAD;
  else if (estado === 'listo' && franjas.length > 0 && disponibles > 0) mensajeEstado = textoFranjasDisponibles(disponibles);

  return (
    <fieldset className="reservas-personal-grupo" aria-describedby={mensajeCampo ? 'franja-error' : undefined}>
      <legend ref={legendRef} tabIndex={-1} className="reservas-campo-etiqueta">Franja</legend>

      <p className="reservas-personal-estado-franjas" role="status" aria-live="polite">{mensajeEstado}</p>

      {estado === 'error' && (
        <div className="reservas-personal-bloque-error">
          <Aviso tipo="error">{error}</Aviso>
          <button
            type="button"
            className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
            onClick={onReintentar}
          >{TEXTO_REINTENTAR}</button>
        </div>
      )}

      {estado === 'listo' && franjas.length === 0 && <Aviso tipo="neutro">{TEXTO_SIN_FRANJAS_ACTIVAS}</Aviso>}
      {estado === 'listo' && franjas.length > 0 && disponibles === 0 && (
        <Aviso tipo="neutro">{TEXTO_SIN_DISPONIBILIDAD_PERSONAL}</Aviso>
      )}

      {mostrarOpciones && franjas.length > 0 && (
        <div className="reservas-personal-opciones">
          {franjas.map((franja) => {
            const deshabilitada = franja.disponible === false;
            return (
              <label
                key={franja.id}
                className={`reservas-personal-opcion${deshabilitada ? ' reservas-personal-opcion--deshabilitada' : ''}`}
              >
                <input
                  type="radio"
                  name="franja-nueva-reserva"
                  value={franja.id}
                  checked={valor === franja.id}
                  disabled={deshabilitada}
                  onChange={() => onCambiar(franja.id)}
                />
                <span>
                  <strong>{franja.nombre} · {formatearHora(franja.horaInicio)}–{formatearHora(franja.horaFin)}</strong>
                  <small>{textoEstado(franja)}</small>
                </span>
              </label>
            );
          })}
        </div>
      )}

      {(estado === 'listo' || estado === 'error') && (
        <button
          type="button"
          className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
          onClick={onReintentar}
        >{TEXTO_ACTUALIZAR_DISPONIBILIDAD}</button>
      )}

      {mensajeCampo && (
        <span id="franja-error" className="reservas-campo-ayuda reservas-campo-ayuda--error" role="alert">{mensajeCampo}</span>
      )}
    </fieldset>
  );
}
