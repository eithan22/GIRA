import { useEffect, useRef, useState } from 'react';
import { MAXIMO_MOTIVO, validarMotivo, validarPersonasReales } from '../reservasPersonalFormato.js';
import {
  AYUDA_MOTIVO,
  CUERPO_CANCELAR,
  CUERPO_CHECKIN,
  CUERPO_COMPLETAR,
  CUERPO_NO_ASISTIO,
  ETIQUETA_MOTIVO,
  ETIQUETA_PERSONAS_REALES,
  TEXTO_ACCION_CANCELAR,
  TEXTO_ACCION_CHECKIN,
  TEXTO_ACCION_MODIFICAR,
  TEXTO_ACCION_COMPLETAR,
  TEXTO_ACCION_NO_ASISTIO,
  TEXTO_CARGANDO_DATOS_MESA,
  TEXTO_CONFIRMAR_CANCELAR,
  TEXTO_CONFIRMAR_CHECKIN,
  TEXTO_CONFIRMAR_COMPLETAR,
  TEXTO_CONFIRMAR_NO_ASISTIO,
  TEXTO_MANTENER_RESERVA,
  TEXTO_VOLVER,
  TITULO_CANCELAR,
  TITULO_CHECKIN,
  TITULO_COMPLETAR,
  TITULO_NO_ASISTIO,
} from '../reservasPersonalTextos.js';

// El cliente solo oculta lo obvio por estado; si la acción procede lo decide el servidor
// (hora, capacidad, concurrencia) y su mensaje se muestra tal cual en el panel.
// «Marcar No asistió» además exige que la franja ya haya empezado (puedeNoAsistio, GI-404).
function accionesPorEstado(estado, puedeNoAsistio) {
  if (estado === 'Confirmada') {
    return puedeNoAsistio
      ? ['checkin', 'modificar', 'noasistio', 'cancelar']
      : ['checkin', 'modificar', 'cancelar'];
  }
  if (estado === 'EnCurso') return ['completar'];
  return [];
}

/**
 * Acciones de una reserva con confirmación en línea (check-in, completar, marcar No asistió, cancelar). Lo que el
 * usuario escribe vive aquí: no depende de la lista ni se pierde cuando la lista se refresca.
 * `onEjecutar(tipo, datos)` hace la llamada y devuelve true si el servidor la aceptó.
 */
export default function AccionesReserva({
  estado, detalle, personasReserva, puedeNoAsistio = false, enviando, onEjecutar, onModificar,
}) {
  const [modo, setModo] = useState(null);
  const [personasTexto, setPersonasTexto] = useState(() => String(personasReserva ?? ''));
  const [motivoTexto, setMotivoTexto] = useState('');

  const personasRef = useRef(null);
  const motivoRef = useRef(null);
  const volverRef = useRef(null);
  const botonCheckInRef = useRef(null);
  const botonCompletarRef = useRef(null);
  const botonNoAsistioRef = useRef(null);
  const botonCancelarRef = useRef(null);
  const devolverFocoARef = useRef(null);

  const disponibles = accionesPorEstado(estado, puedeNoAsistio);
  // Si el estado cambió (por ejemplo tras un 409) y la acción abierta ya no aplica, se cierra sola.
  const modoActivo = disponibles.includes(modo) ? modo : null;

  const capacidad = detalle?.mesaCapacidad ?? null;
  const validacionPersonas = validarPersonasReales(personasTexto, capacidad);
  const validacionMotivo = validarMotivo(motivoTexto);

  useEffect(() => {
    if (modoActivo === 'checkin') personasRef.current?.focus();
    else if (modoActivo === 'cancelar') motivoRef.current?.focus();
    else if (modoActivo === 'completar' || modoActivo === 'noasistio') volverRef.current?.focus();
    else if (devolverFocoARef.current) {
      const botones = {
        checkin: botonCheckInRef, cancelar: botonCancelarRef, completar: botonCompletarRef, noasistio: botonNoAsistioRef,
      };
      botones[devolverFocoARef.current]?.current?.focus();
      devolverFocoARef.current = null;
    }
  }, [modoActivo]);

  function cerrar(tipo) {
    devolverFocoARef.current = tipo;
    setModo(null);
  }

  async function confirmar(tipo, datos) {
    const aceptada = await onEjecutar(tipo, datos);
    if (aceptada) {
      setModo(null);
      setMotivoTexto('');
    }
  }

  if (disponibles.length === 0) return null;

  if (modoActivo === 'checkin') {
    const puedeConfirmar = Boolean(detalle) && validacionPersonas.valido && !enviando;
    return (
      <section className="reservas-personal-confirmacion" aria-labelledby="accion-titulo">
        <h3 id="accion-titulo" className="reservas-personal-confirmacion-titulo">{TITULO_CHECKIN}</h3>
        <p className="reservas-personal-confirmacion-texto">{CUERPO_CHECKIN}</p>
        <div className="reservas-campo">
          <label className="reservas-campo-etiqueta" htmlFor="accion-personas">{ETIQUETA_PERSONAS_REALES}</label>
          <input
            id="accion-personas"
            ref={personasRef}
            type="number"
            inputMode="numeric"
            min={1}
            max={capacidad ?? undefined}
            step={1}
            autoComplete="off"
            className="reservas-input"
            value={personasTexto}
            onChange={(evento) => setPersonasTexto(evento.target.value)}
            aria-invalid={personasTexto !== '' && !validacionPersonas.valido ? 'true' : undefined}
            aria-describedby="accion-personas-ayuda"
          />
          <span
            id="accion-personas-ayuda"
            className={`reservas-campo-ayuda${personasTexto !== '' && !validacionPersonas.valido ? ' reservas-campo-ayuda--error' : ''}`}
          >
            {!detalle
              ? TEXTO_CARGANDO_DATOS_MESA
              : personasTexto !== '' && !validacionPersonas.valido
                ? validacionPersonas.mensaje
                : `Entre 1 y ${capacidad} (capacidad de la mesa).`}
          </span>
        </div>
        <div className="reservas-personal-confirmacion-botones">
          <button
            type="button"
            className="reservas-boton reservas-boton--chico reservas-boton--primario reservas-personal-boton-auto"
            onClick={() => confirmar('checkin', { personasReales: validacionPersonas.valor })}
            disabled={!puedeConfirmar}
          >{enviando ? 'Registrando…' : TEXTO_CONFIRMAR_CHECKIN}</button>
          <button
            type="button"
            className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
            onClick={() => cerrar('checkin')}
            disabled={enviando}
          >{TEXTO_VOLVER}</button>
        </div>
      </section>
    );
  }

  if (modoActivo === 'completar') {
    return (
      <section className="reservas-personal-confirmacion" aria-labelledby="accion-titulo">
        <h3 id="accion-titulo" className="reservas-personal-confirmacion-titulo">{TITULO_COMPLETAR}</h3>
        <p className="reservas-personal-confirmacion-texto">{CUERPO_COMPLETAR}</p>
        <div className="reservas-personal-confirmacion-botones">
          <button
            type="button"
            className="reservas-boton reservas-boton--chico reservas-boton--primario reservas-personal-boton-auto"
            onClick={() => confirmar('completar')}
            disabled={enviando}
          >{enviando ? 'Completando…' : TEXTO_CONFIRMAR_COMPLETAR}</button>
          <button
            type="button"
            ref={volverRef}
            className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
            onClick={() => cerrar('completar')}
            disabled={enviando}
          >{TEXTO_VOLVER}</button>
        </div>
      </section>
    );
  }

  if (modoActivo === 'noasistio') {
    return (
      <section className="reservas-personal-confirmacion reservas-personal-confirmacion--peligro" aria-labelledby="accion-titulo">
        <h3 id="accion-titulo" className="reservas-personal-confirmacion-titulo">{TITULO_NO_ASISTIO}</h3>
        <p className="reservas-personal-confirmacion-texto">{CUERPO_NO_ASISTIO}</p>
        <div className="reservas-personal-confirmacion-botones">
          <button
            type="button"
            className="reservas-boton reservas-boton--chico reservas-boton--peligro reservas-personal-boton-auto"
            onClick={() => confirmar('noasistio')}
            disabled={enviando}
          >{enviando ? 'Marcando…' : TEXTO_CONFIRMAR_NO_ASISTIO}</button>
          <button
            type="button"
            ref={volverRef}
            className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
            onClick={() => cerrar('noasistio')}
            disabled={enviando}
          >{TEXTO_VOLVER}</button>
        </div>
      </section>
    );
  }

  if (modoActivo === 'cancelar') {
    const longitud = motivoTexto.trim().length;
    const excede = longitud > MAXIMO_MOTIVO;
    return (
      <section className="reservas-personal-confirmacion reservas-personal-confirmacion--peligro" aria-labelledby="accion-titulo">
        <h3 id="accion-titulo" className="reservas-personal-confirmacion-titulo">{TITULO_CANCELAR}</h3>
        <p className="reservas-personal-confirmacion-texto">{CUERPO_CANCELAR}</p>
        <div className="reservas-campo">
          <label className="reservas-campo-etiqueta" htmlFor="accion-motivo">{ETIQUETA_MOTIVO}</label>
          <textarea
            id="accion-motivo"
            ref={motivoRef}
            rows={4}
            autoComplete="off"
            className="reservas-input reservas-personal-textarea"
            value={motivoTexto}
            onChange={(evento) => setMotivoTexto(evento.target.value)}
            aria-invalid={excede ? 'true' : undefined}
            aria-describedby="accion-motivo-ayuda accion-motivo-contador"
          />
          <div className="reservas-personal-contador-fila">
            <span id="accion-motivo-ayuda" className="reservas-campo-ayuda">{AYUDA_MOTIVO}</span>
            <span
              id="accion-motivo-contador"
              className={`reservas-campo-ayuda${excede ? ' reservas-campo-ayuda--error' : ''}`}
            >{longitud}/{MAXIMO_MOTIVO}</span>
          </div>
        </div>
        <div className="reservas-personal-confirmacion-botones">
          <button
            type="button"
            className="reservas-boton reservas-boton--chico reservas-boton--peligro reservas-personal-boton-auto"
            onClick={() => confirmar('cancelar', { motivo: validacionMotivo.motivo })}
            disabled={!validacionMotivo.valido || enviando}
          >{enviando ? 'Cancelando…' : TEXTO_CONFIRMAR_CANCELAR}</button>
          <button
            type="button"
            className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
            onClick={() => cerrar('cancelar')}
            disabled={enviando}
          >{TEXTO_MANTENER_RESERVA}</button>
        </div>
      </section>
    );
  }

  return (
    <div className="reservas-personal-acciones">
      {disponibles.includes('checkin') && (
        <button
          type="button"
          ref={botonCheckInRef}
          className="reservas-boton reservas-boton--chico reservas-boton--primario reservas-personal-boton-auto"
          onClick={() => setModo('checkin')}
        >{TEXTO_ACCION_CHECKIN}</button>
      )}
      {disponibles.includes('modificar') && onModificar && (
        <button
          type="button"
          className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
          onClick={onModificar}
          disabled={!detalle}
        >{TEXTO_ACCION_MODIFICAR}</button>
      )}
      {disponibles.includes('completar') && (
        <button
          type="button"
          ref={botonCompletarRef}
          className="reservas-boton reservas-boton--chico reservas-boton--primario reservas-personal-boton-auto"
          onClick={() => setModo('completar')}
        >{TEXTO_ACCION_COMPLETAR}</button>
      )}
      {disponibles.includes('noasistio') && (
        <button
          type="button"
          ref={botonNoAsistioRef}
          className="reservas-boton reservas-boton--chico reservas-boton--secundario reservas-personal-boton-auto"
          onClick={() => setModo('noasistio')}
        >{TEXTO_ACCION_NO_ASISTIO}</button>
      )}
      {disponibles.includes('cancelar') && (
        <button
          type="button"
          ref={botonCancelarRef}
          className="reservas-boton reservas-boton--chico reservas-boton--fantasma-error reservas-personal-boton-auto"
          onClick={() => setModo('cancelar')}
        >{TEXTO_ACCION_CANCELAR}</button>
      )}
    </div>
  );
}
